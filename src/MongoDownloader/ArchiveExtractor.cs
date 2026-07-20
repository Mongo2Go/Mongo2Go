using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ByteSizeLib;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;

namespace MongoDownloader
{
    internal class ArchiveExtractor
    {
        private readonly Options _options;
        private readonly BinaryStripper? _binaryStripper;

        public ArchiveExtractor(Options options, BinaryStripper? binaryStripper)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _binaryStripper = binaryStripper;
        }

        /// <summary>
        /// Extracts the binaries and licence files from an archive that has already been downloaded and verified.
        /// </summary>
        /// <remarks>
        /// ZIP archives were previously read directly over HTTP with range requests, which never held the whole
        /// archive and so left nothing to checksum. Both formats are now extracted from a local file, so the archive
        /// can be verified against MongoDB's published SHA-256 before a single entry is read.
        /// </remarks>
        public async Task<IEnumerable<Task<ByteSize>>> ExtractArchiveAsync(Download download, FileInfo archive, DirectoryInfo extractDirectory, CancellationToken cancellationToken)
        {
            switch (Path.GetExtension(archive.Name))
            {
                case ".tgz":
                    return ExtractTarGzipArchive(download, archive, extractDirectory, cancellationToken);
                case ".zip":
                    return await ExtractZipArchiveAsync(download, archive, extractDirectory, cancellationToken);
                default:
                    throw new NotSupportedException($"Only .tgz and .zip archives are currently supported. \"{archive.FullName}\" can not be extracted.");
            }
        }

        private async Task<IEnumerable<Task<ByteSize>>> ExtractZipArchiveAsync(Download download, FileInfo archive, DirectoryInfo extractDirectory, CancellationToken cancellationToken)
        {
            await using var archiveStream = archive.OpenRead();
            using var zipFile = new ZipFile(archiveStream);
            var binaryRegex = _options.Binaries[(download.Product, download.Platform)];
            var licenseRegex = _options.Licenses[(download.Product, download.Platform)];
            var stripTasks = new List<Task<ByteSize>>();
            foreach (var entry in zipFile.Cast<ZipEntry>().Where(e => e.IsFile))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var nameParts = entry.Name.Split('\\', '/').Skip(1).ToList();
                var zipEntryPath = string.Join('/', nameParts);
                var isBinaryFile = binaryRegex.IsMatch(zipEntryPath);
                var isLicenseFile = licenseRegex.IsMatch(zipEntryPath);
                if (isBinaryFile || isLicenseFile)
                {
                    var destinationPathParts = isLicenseFile ? nameParts.Prepend(ProductDirectoryName(download.Product)) : nameParts;
                    var destinationFile = ResolveContainedFile(extractDirectory, destinationPathParts);
                    destinationFile.Directory?.Create();
                    await using var destinationStream = new FileStream(destinationFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None);
                    await using var inputStream = zipFile.GetInputStream(entry);
                    await inputStream.CopyToAsync(destinationStream, cancellationToken);
                    if (isBinaryFile && _binaryStripper is not null)
                    {
                        stripTasks.Add(_binaryStripper.StripAsync(destinationFile, cancellationToken));
                    }
                }
            }
            return stripTasks;
        }

        private IEnumerable<Task<ByteSize>> ExtractTarGzipArchive(Download download, FileInfo archive, DirectoryInfo extractDirectory, CancellationToken cancellationToken)
        {
            // See https://github.com/icsharpcode/SharpZipLib/wiki/GZip-and-Tar-Samples#-simple-full-extract-from-a-tgz-targz
            using var archiveStream = archive.OpenRead();
            using var gzipStream = new GZipInputStream(archiveStream);
            using var tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.UTF8);
            var extractedFileNames = new List<string>();
            tarArchive.ProgressMessageEvent += (_, entry, _) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                extractedFileNames.Add(entry.Name);
            };
            tarArchive.ExtractContents(extractDirectory.FullName);
            return CleanupExtractedFiles(download, extractDirectory, extractedFileNames);
        }

        private IEnumerable<Task<ByteSize>> CleanupExtractedFiles(Download download, DirectoryInfo extractDirectory, IEnumerable<string> extractedFileNames)
        {
            var rootDirectoryToDelete = new HashSet<string>();
            var binaryRegex = _options.Binaries[(download.Product, download.Platform)];
            var licenseRegex = _options.Licenses[(download.Product, download.Platform)];
            var stripTasks = new List<Task<ByteSize>>();
            foreach (var extractedFileName in extractedFileNames.Select(e => e.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)))
            {
                // Tar entry names may be absolute (SharpZipLib re-roots them on extraction but reports them verbatim),
                // in which case an unchecked Path.Combine would discard extractDirectory and target a real host path.
                var extractedFile = ResolveContainedFile(extractDirectory, new[] { extractedFileName });
                var parts = extractedFileName.Split(Path.DirectorySeparatorChar);
                var entryFileName = string.Join("/", parts.Skip(1));
                rootDirectoryToDelete.Add(parts[0]);
                var isBinaryFile = binaryRegex.IsMatch(entryFileName);
                var isLicenseFile = licenseRegex.IsMatch(entryFileName);
                if (!(isBinaryFile || isLicenseFile))
                {
                    extractedFile.Delete();
                }
                else
                {
                    var destinationPathParts = parts.Skip(1);
                    if (isLicenseFile)
                    {
                        destinationPathParts = destinationPathParts.Prepend(ProductDirectoryName(download.Product));
                    }
                    var destinationFile = ResolveContainedFile(extractDirectory, destinationPathParts);
                    destinationFile.Directory?.Create();
                    extractedFile.MoveTo(destinationFile.FullName);
                    if (isBinaryFile && _binaryStripper is not null)
                    {
                        stripTasks.Add(_binaryStripper.StripAsync(destinationFile));
                    }
                }
            }
            var rootArchiveDirectory = new DirectoryInfo(Path.Combine(extractDirectory.FullName, rootDirectoryToDelete.Single()));
            var binDirectory = new DirectoryInfo(Path.Combine(rootArchiveDirectory.FullName, "bin"));
            binDirectory.Delete(recursive: false);
            rootArchiveDirectory.Delete(recursive: false);
            return stripTasks;
        }

        /// <summary>
        /// Combines <paramref name="pathParts"/> onto <paramref name="extractDirectory"/> and guarantees the result stays
        /// inside it.
        /// </summary>
        /// <remarks>
        /// Archive entry names are attacker-controlled data: they arrive from a downloaded archive and are not validated
        /// by the archive libraries. Two distinct escapes are possible without this check:
        /// <list type="bullet">
        /// <item>a relative entry containing <c>..</c> segments, because <see cref="Path.Combine(string[])"/> does not
        /// normalise or reject them ("zip slip");</item>
        /// <item>a rooted entry such as <c>/etc/passwd</c>, because <see cref="Path.Combine(string[])"/> discards every
        /// preceding segment as soon as one is rooted.</item>
        /// </list>
        /// Both are resolved by <see cref="Path.GetFullPath(string)"/> and then rejected by the prefix comparison.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The entry resolves outside <paramref name="extractDirectory"/>.</exception>
        private static FileInfo ResolveContainedFile(DirectoryInfo extractDirectory, IEnumerable<string> pathParts)
        {
            var root = Path.GetFullPath(extractDirectory.FullName);
            var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;

            var combined = Path.Combine(pathParts.Prepend(root).ToArray());
            var resolved = Path.GetFullPath(combined);

            if (!resolved.StartsWith(rootWithSeparator, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Refusing to extract an archive entry that escapes the extraction directory. " +
                    $"The entry resolves to \"{resolved}\" which is outside \"{root}\". " +
                    $"This indicates a malicious or corrupted archive.");
            }

            return new FileInfo(resolved);
        }

        private static string ProductDirectoryName(Product product)
        {
            return product switch
            {
                Product.CommunityServer => "community-server",
                Product.DatabaseTools => "database-tools",
                _ => throw new ArgumentOutOfRangeException(nameof(product), product, null)
            };
        }
    }
}