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

        public ArchiveExtractor(Options options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task DownloadExtractZipArchiveAsync(Download download, DirectoryInfo extractDirectory, DownloadProgress progress, CancellationToken cancellationToken)
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
            foreach (var entry in zipFile.Cast<ZipEntry>().Where(e => e.IsFile))
            {
                var nameParts = entry.Name.Split('\\', '/').Skip(1).ToList();
                var zipEntryPath = string.Join('/', nameParts);
                var isBinaryFile = binaryRegex.IsMatch(zipEntryPath);
                var isLicenseFile = licenseRegex.IsMatch(zipEntryPath);
                if (isBinaryFile || isLicenseFile)
                {
                    var destinationPathParts = isLicenseFile ? nameParts.Prepend(ProductDirectoryName(download.Product)) : nameParts;
                    var destinationPath = Path.Combine(destinationPathParts.Prepend(extractDirectory.FullName).ToArray());
                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    await using var destinationStream = File.Create(destinationPath);
                    await using var inputStream = zipFile.GetInputStream(entry);
                    await inputStream.CopyToAsync(destinationStream, cancellationToken);
                }
            }
            progress.Report(new CopyProgress(stopwatch.Elapsed, 0, bytesTransferred, bytesTransferred));
        }

        public void ExtractArchive(Download download, FileInfo archive, DirectoryInfo extractDirectory, CancellationToken cancellationToken)
        {
            switch (Path.GetExtension(archive.Name))
            {
                case ".tgz":
                    ExtractTarGzipArchive(download, archive, extractDirectory, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"Only .tgz archives are currently supported. \"{archive.FullName}\" can not be extracted.");
            }
        }

        private void ExtractTarGzipArchive(Download download, FileInfo archive, DirectoryInfo extractDirectory, CancellationToken cancellationToken)
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
            CleanupExtractedFiles(download, extractDirectory, extractedFileNames);
        }

        private void CleanupExtractedFiles(Download download, DirectoryInfo extractDirectory, IEnumerable<string> extractedFileNames)
        {
            var rootDirectoryToDelete = new HashSet<string>();
            var binaryRegex = _options.Binaries[(download.Product, download.Platform)];
            var licenseRegex = _options.Licenses[(download.Product, download.Platform)];
            foreach (var extractedFileName in extractedFileNames.Select(e => e.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)))
            {
                var extractedFile = new FileInfo(Path.Combine(extractDirectory.FullName, extractedFileName));
                var parts = extractedFileName.Split(Path.DirectorySeparatorChar);
                var entryFileName = string.Join("/", parts.Skip(1));
                rootDirectoryToDelete.Add(parts[0]);
                if (!(binaryRegex.IsMatch(entryFileName) || licenseRegex.IsMatch(entryFileName)))
                {
                    extractedFile.Delete();
                }
                else
                {
                    var destinationPathParts = parts.Skip(1);
                    if (licenseRegex.IsMatch(entryFileName))
                    {
                        destinationPathParts = destinationPathParts.Prepend(ProductDirectoryName(download.Product));
                    }
                    var destinationPath = new FileInfo(Path.Combine(destinationPathParts.Prepend(extractDirectory.FullName).ToArray()));
                    destinationPath.Directory?.Create();
                    extractedFile.MoveTo(destinationPath.FullName);
                }
            }
            var rootArchiveDirectory = new DirectoryInfo(Path.Combine(extractDirectory.FullName, rootDirectoryToDelete.Single()));
            var binDirectory = new DirectoryInfo(Path.Combine(rootArchiveDirectory.FullName, "bin"));
            binDirectory.Delete(recursive: false);
            rootArchiveDirectory.Delete(recursive: false);
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