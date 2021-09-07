using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ByteSizeLib;
using Espresso3389.HttpStream;
using HttpProgress;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;

namespace MongoDownloader
{
    internal class ArchiveExtractor
    {
        private static readonly int CachePageSize = Convert.ToInt32(ByteSize.FromMebiBytes(4).Bytes);

        private readonly Options _options;
        private readonly BinaryStripper? _binaryStripper;

        public ArchiveExtractor(Options options, BinaryStripper? binaryStripper)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _binaryStripper = binaryStripper;
        }

        public async Task<IEnumerable<Task<ByteSize>>> DownloadExtractZipArchiveAsync(Download download, DirectoryInfo extractDirectory, ArchiveProgress progress, CancellationToken cancellationToken)
        {
            var bytesTransferred = 0L;
            using var headResponse = await _options.HttpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, download.Archive.Url), cancellationToken);
            var contentLength = headResponse.Content.Headers.ContentLength ?? 0;
            var cacheFile = new FileInfo(Path.Combine(_options.CacheDirectory.FullName, download.Archive.Url.Segments.Last()));
            await using var cacheStream = new FileStream(cacheFile.FullName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var stopwatch = Stopwatch.StartNew();
            await using var httpStream = new HttpStream(download.Archive.Url, cacheStream, ownStream: false, CachePageSize, cached: null);
            httpStream.RangeDownloaded += (_, args) =>
            {
                bytesTransferred += args.Length;
                progress.Report(new CopyProgress(stopwatch.Elapsed, 0, bytesTransferred, contentLength));
            };
            using var zipFile = new ZipFile(httpStream);
            var binaryRegex = _options.Binaries[(download.Product, download.Platform)];
            var licenseRegex = _options.Licenses[(download.Product, download.Platform)];
            var stripTasks = new List<Task<ByteSize>>();
            foreach (var entry in zipFile.Cast<ZipEntry>().Where(e => e.IsFile))
            {
                var nameParts = entry.Name.Split('\\', '/').Skip(1).ToList();
                var zipEntryPath = string.Join('/', nameParts);
                var isBinaryFile = binaryRegex.IsMatch(zipEntryPath);
                var isLicenseFile = licenseRegex.IsMatch(zipEntryPath);
                if (isBinaryFile || isLicenseFile)
                {
                    var destinationPathParts = isLicenseFile ? nameParts.Prepend(ProductDirectoryName(download.Product)) : nameParts;
                    var destinationFile = new FileInfo(Path.Combine(destinationPathParts.Prepend(extractDirectory.FullName).ToArray()));
                    destinationFile.Directory?.Create();
                    await using var destinationStream = destinationFile.OpenWrite();
                    await using var inputStream = zipFile.GetInputStream(entry);
                    await inputStream.CopyToAsync(destinationStream, cancellationToken);
                    if (isBinaryFile && _binaryStripper is not null)
                    {
                        stripTasks.Add(_binaryStripper.StripAsync(destinationFile, cancellationToken));
                    }
                }
            }
            progress.Report(new CopyProgress(stopwatch.Elapsed, 0, bytesTransferred, bytesTransferred));
            return stripTasks;
        }

        public IEnumerable<Task<ByteSize>> ExtractArchive(Download download, FileInfo archive, DirectoryInfo extractDirectory, CancellationToken cancellationToken)
        {
            switch (Path.GetExtension(archive.Name))
            {
                case ".tgz":
                    return ExtractTarGzipArchive(download, archive, extractDirectory, cancellationToken);
                default:
                    throw new NotSupportedException($"Only .tgz archives are currently supported. \"{archive.FullName}\" can not be extracted.");
            }
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
                var extractedFile = new FileInfo(Path.Combine(extractDirectory.FullName, extractedFileName));
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
                    var destinationFile = new FileInfo(Path.Combine(destinationPathParts.Prepend(extractDirectory.FullName).ToArray()));
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