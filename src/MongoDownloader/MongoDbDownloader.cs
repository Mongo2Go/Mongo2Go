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

        public async Task RunAsync(DirectoryInfo toolsDirectory, CancellationToken cancellationToken)
        {
            await AnsiConsole
                .Progress()
                .Columns(
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new DownloadedColumn(),
                    new TaskDescriptionColumn { Alignment = Justify.Left }
                )
                .StartAsync(async context => await RunAsync(context, toolsDirectory, cancellationToken));
        }

        private async Task RunAsync(ProgressContext context, DirectoryInfo toolsDirectory, CancellationToken cancellationToken)
        {
            const double initialMaxValue = double.Epsilon;
            var globalProgress = context.AddTask("Downloading MongoDB", maxValue: initialMaxValue);

            var (communityServerVersion, communityServerDownloads) = await GetCommunityServerDownloadsAsync(cancellationToken);
            globalProgress.Description = $"Downloading MongoDB Community Server {communityServerVersion.Number}";

            var (databaseToolsVersion, databaseToolsDownloads) = await GetDatabaseToolsDownloadsAsync(cancellationToken);
            globalProgress.Description = $"Downloading MongoDB Community Server {communityServerVersion.Number} and Database Tools {databaseToolsVersion.Number}";

            var tasks = new List<Task>();
            var allArchiveProgresses = new HashSet<ProgressTask>();
            foreach (var download in communityServerDownloads.Concat(databaseToolsDownloads))
            {
                var archiveProgress = context.AddTask($"Downloading {download.Product} for {download.Platform} from {download.Archive.Url}", maxValue: initialMaxValue);
                var directoryName = $"mongodb-{download.Platform.ToString().ToLowerInvariant()}-{communityServerVersion.Number}-database-tools-{databaseToolsVersion.Number}";
                var extractDirectory = new DirectoryInfo(Path.Combine(toolsDirectory.FullName, directoryName));
                tasks.Add(ProcessArchiveAsync(download, extractDirectory, globalProgress, archiveProgress, allArchiveProgresses, cancellationToken));
            }
            await Task.WhenAll(tasks);
            globalProgress.Description = $"✅ Downloaded and extracted MongoDB Community Server {communityServerVersion.Number} and Database Tools {databaseToolsVersion.Number} into {new Uri(toolsDirectory.FullName).AbsoluteUri}";
            globalProgress.Value = globalProgress.MaxValue;
        }

        private async Task ProcessArchiveAsync(Download download, DirectoryInfo extractDirectory, ProgressTask globalProgress, ProgressTask archiveProgress, ISet<ProgressTask> allArchiveProgresses, CancellationToken cancellationToken)
        {
            string Message(ICopyProgress progress)
            {
                var speed = ByteSize.FromBytes(progress.BytesTransferred / progress.TransferTime.TotalSeconds);
                return $"Downloading {download.Product} for {download.Platform} from {download.Archive.Url} at {speed.ToString("0.0")}/s";
            }
            double? Percentage(ICopyProgress progress)
            {
                lock (allArchiveProgresses)
                {
                    allArchiveProgresses.Add(archiveProgress);
                    globalProgress.Value = allArchiveProgresses.Sum(e => e.Value);
                    globalProgress.MaxValue = allArchiveProgresses.Sum(e => e.MaxValue);
                }
                archiveProgress.MaxValue = progress.ExpectedBytes;
                return _options.DownloadExtractRatio * progress.PercentComplete;
            }
            var downloadProgress = archiveProgress.AsProgress<ICopyProgress>(Message, Percentage);
            var archiveFileInfo = await DownloadArchiveAsync(download.Archive, downloadProgress, cancellationToken);

            archiveProgress.Description = $"Extracting {download.Product} for {download.Platform}";
            archiveProgress.IsIndeterminate = true;
            lock (allArchiveProgresses)
            {
                globalProgress.IsIndeterminate = allArchiveProgresses.All(e => e.IsFinished || e.IsIndeterminate);
            }
            _extractor.ExtractArchive(download, archiveFileInfo, extractDirectory, cancellationToken);
            archiveProgress.Description = $"Extracted {download.Product} for {download.Platform}";
            archiveProgress.Value = archiveProgress.MaxValue;
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
            var downloads = Enum.GetValues<Platform>().Select(e => GetDownload(e, Product.CommunityServer, version, _options.PlatformIdentifiers[e], _options.Architecture, _options.Edition));
            return (version, downloads);
        }

        private async Task<(Version version, IEnumerable<Download> downloads)> GetDatabaseToolsDownloadsAsync(CancellationToken cancellationToken)
        {
            var release = await _options.HttpClient.GetFromJsonAsync<Release>(_options.DatabaseToolsUrl, cancellationToken) ?? throw new InvalidOperationException($"Failed to deserialize {nameof(Release)}");
            var version = release.Versions.FirstOrDefault() ?? throw new InvalidOperationException("No Database Tools version was found");
            var downloads = Enum.GetValues<Platform>().Select(e => GetDownload(e, Product.DatabaseTools, version, _options.PlatformIdentifiers[e], _options.Architecture));
            return (version, downloads);
        }

        private static Download GetDownload(Platform platform, Product product, Version version, Regex platformRegex, Regex architectureRegex, Regex? editionRegex = null)
        {
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