using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ByteSizeLib;
using HttpProgress;
using Spectre.Console;

namespace MongoDownloader
{
    internal class MongoDbDownloader
    {
        private readonly ArchiveExtractor _extractor;
        private readonly Options _options;

        public MongoDbDownloader(ArchiveExtractor extractor, Options options)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<ByteSize> RunAsync(DirectoryInfo toolsDirectory, CancellationToken cancellationToken)
        {
            var strippedSize = await AnsiConsole
                .Progress()
                .Columns(
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new DownloadedColumn(),
                    new TaskDescriptionColumn { Alignment = Justify.Left }
                )
                .StartAsync(async context => await RunAsync(context, toolsDirectory, cancellationToken));

            return strippedSize;
        }

        private async Task<ByteSize> RunAsync(ProgressContext context, DirectoryInfo toolsDirectory, CancellationToken cancellationToken)
        {
            const double initialMaxValue = double.Epsilon;
            var globalProgress = context.AddTask("Downloading MongoDB", maxValue: initialMaxValue);

            var (communityServerVersion, communityServerDownloads) = await GetCommunityServerDownloadsAsync(cancellationToken);
            globalProgress.Description = $"Downloading MongoDB Community Server {communityServerVersion.Number}";

            var (databaseToolsVersion, databaseToolsDownloads) = await GetDatabaseToolsDownloadsAsync(cancellationToken);
            globalProgress.Description = $"Downloading MongoDB Community Server {communityServerVersion.Number} and Database Tools {databaseToolsVersion.Number}";

            var tasks = new List<Task<ByteSize>>();
            var allArchiveProgresses = new List<ProgressTask>();
            foreach (var download in communityServerDownloads.Concat(databaseToolsDownloads))
            {
                var archiveProgress = context.AddTask($"Downloading {download.Product} for {download.Platform} from {download.Archive.Url}", maxValue: initialMaxValue);
                var directoryName = $"mongodb-{download.Platform.ToString().ToLowerInvariant()}-{communityServerVersion.Number}-database-tools-{databaseToolsVersion.Number}";
                var extractDirectory = new DirectoryInfo(Path.Combine(toolsDirectory.FullName, directoryName));
                allArchiveProgresses.Add(archiveProgress);
                var progress = new ArchiveProgress(archiveProgress, globalProgress, allArchiveProgresses, download, $"✅ Downloaded and extracted MongoDB Community Server {communityServerVersion.Number} and Database Tools {databaseToolsVersion.Number} into {new Uri(toolsDirectory.FullName).AbsoluteUri}");
                tasks.Add(ProcessArchiveAsync(download, extractDirectory, progress, cancellationToken));
            }
            var strippedSizes = await Task.WhenAll(tasks);
            return strippedSizes.Aggregate(new ByteSize(0), (current, strippedSize) => current + strippedSize);
        }

        private async Task<ByteSize> ProcessArchiveAsync(Download download, DirectoryInfo extractDirectory, ArchiveProgress progress, CancellationToken cancellationToken)
        {
            IEnumerable<Task<ByteSize>> stripTasks;
            var archiveExtension = Path.GetExtension(download.Archive.Url.AbsolutePath);
            if (archiveExtension == ".zip")
            {
                stripTasks = await _extractor.DownloadExtractZipArchiveAsync(download, extractDirectory, progress, cancellationToken);
            }
            else
            {
                var archiveFileInfo = await DownloadArchiveAsync(download.Archive, progress, cancellationToken);
                stripTasks = _extractor.ExtractArchive(download, archiveFileInfo, extractDirectory, cancellationToken);
            }
            progress.Report("Stripping binaries");
            var completedStripTasks = await Task.WhenAll(stripTasks);
            var totalStrippedSize = completedStripTasks.Aggregate(new ByteSize(0), (current, strippedSize) => current + strippedSize);
            progress.ReportCompleted(totalStrippedSize);
            return totalStrippedSize;
        }

        private async Task<FileInfo> DownloadArchiveAsync(Archive archive, IProgress<ICopyProgress> progress, CancellationToken cancellationToken)
        {
            _options.CacheDirectory.Create();
            var destinationFile = new FileInfo(Path.Combine(_options.CacheDirectory.FullName, archive.Url.Segments.Last()));
            var useCache = bool.TryParse(Environment.GetEnvironmentVariable("MONGO2GO_DOWNLOADER_USE_CACHED_FILE") ?? "", out var useCachedFile) && useCachedFile;
            if (useCache && destinationFile.Exists)
            {
                progress.Report(new CopyProgress(TimeSpan.Zero, 0, 1, 1));
                return destinationFile;
            }
            await using var destinationStream = destinationFile.OpenWrite();
            await _options.HttpClient.GetAsync(archive.Url.AbsoluteUri, destinationStream, progress, cancellationToken);
            return destinationFile;
        }

        private async Task<(Version version, IEnumerable<Download> downloads)> GetCommunityServerDownloadsAsync(CancellationToken cancellationToken)
        {
            var release = await _options.HttpClient.GetFromJsonAsync<Release>(_options.CommunityServerUrl, cancellationToken) ?? throw new InvalidOperationException($"Failed to deserialize {nameof(Release)}");
            var version = release.Versions.FirstOrDefault(e => e.Production) ?? throw new InvalidOperationException("No Community Server production version was found");
            var downloads = Enum.GetValues<Platform>().Select(e => GetDownload(e, Product.CommunityServer, version, _options.PlatformIdentifiers[e], _options.Architecture[e], _options.Edition));
            return (version, downloads);
        }

        private async Task<(Version version, IEnumerable<Download> downloads)> GetDatabaseToolsDownloadsAsync(CancellationToken cancellationToken)
        {
            var release = await _options.HttpClient.GetFromJsonAsync<Release>(_options.DatabaseToolsUrl, cancellationToken) ?? throw new InvalidOperationException($"Failed to deserialize {nameof(Release)}");
            var version = release.Versions.FirstOrDefault() ?? throw new InvalidOperationException("No Database Tools version was found");
            var downloads = Enum.GetValues<Platform>().Select(e => GetDownload(e, Product.DatabaseTools, version, _options.PlatformIdentifiers[e], _options.Architecture[e]));
            return (version, downloads);
        }

        private static Download GetDownload(Platform platform, Product product, Version version, Regex platformRegex, Regex architectureRegex, Regex? editionRegex = null)
        {
            //For some reason, mongo set the name of the architecture different for the database tools than the server
            if (product == Product.DatabaseTools && platform == Platform.LinuxARM64) 
                architectureRegex = new Regex("arm64");
            var download = version.Downloads.LastOrDefault(e => (platformRegex.IsMatch(e.Target) || platformRegex.IsMatch(e.Name)) && architectureRegex.IsMatch(e.Architecture) && (editionRegex?.IsMatch(e.Edition) ?? true));
            if (download == null)
            {
                throw new InvalidOperationException($"{platformRegex}/{editionRegex}/{architectureRegex} download not found");
            }
            download.Platform = platform;
            download.Product = product;
            return download;
        }
    }
}