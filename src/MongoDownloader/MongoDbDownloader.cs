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
using ShellProgressBar;

namespace MongoDownloader
{
    internal class MongoDbDownloader
    {
        private static readonly IEnumerable<Platform> SupportedPlatforms = new[] { Platform.Linux, Platform.macOS, Platform.Windows };

        private readonly ArchiveExtractor _extractor;
        private readonly Options _options;

        public MongoDbDownloader(ArchiveExtractor extractor, Options options)
        {
            _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task RunAsync(DirectoryInfo toolsDirectory, CancellationToken cancellationToken = default)
        {
            var progressBarOptions = new ProgressBarOptions
            {
                ProgressCharacter = '─',
                ProgressBarOnBottom = true,
                ForegroundColor = Console.BackgroundColor switch
                {
                    ConsoleColor.Black => ConsoleColor.White,
                    ConsoleColor.White => ConsoleColor.Black,
                    _ => ConsoleColor.Green,
                },
            };
            const int ticks = 6; // 3 (Linux + macOS + Windows) * 2 (CommunityServer + DatabaseTools)
            using var progressBar = new ProgressBar(ticks, "Downloading MongoDB", progressBarOptions);

            var (communityServerVersion, communityServerDownloads) = await GetCommunityServerDownloadsAsync(cancellationToken);
            progressBar.Message = $"Downloading MongoDB Community Server {communityServerVersion.Number}";

            var (databaseToolsVersion, databaseToolsDownloads) = await GetDatabaseToolsDownloadsAsync(cancellationToken);
            progressBar.Message = $"Downloading MongoDB Community Server {communityServerVersion.Number} and Database Tools {databaseToolsVersion.Number}";

            var tasks = new List<Task>();
            foreach (var download in communityServerDownloads.Concat(databaseToolsDownloads))
            {
                using var downloadProgressBar = progressBar.Spawn(1000, $"Downloading {download.Product} for {download.Platform} archive from {download.Archive.Url}");
                var directoryName = $"mongodb-{download.Platform.ToString().ToLowerInvariant()}-{communityServerVersion.Number}-database-tools-{databaseToolsVersion.Number}";
                var extractDirectory = new DirectoryInfo(Path.Combine(toolsDirectory.FullName, directoryName));
                tasks.Add(ProcessArchiveAsync(download, extractDirectory, progressBar, downloadProgressBar, cancellationToken));
            }

            await Task.WhenAll(tasks);
        }

        private async Task ProcessArchiveAsync(Download download, DirectoryInfo extractDirectory, ProgressBar mainProgressBar, ChildProgressBar archiveProgressBar, CancellationToken cancellationToken)
        {
            string Message(ICopyProgress progress)
            {
                var speed = ByteSize.FromBytes(progress.BytesTransferred / progress.TransferTime.TotalSeconds);
                return $"Downloading {download.Product} for {download.Platform} archive from {download.Archive.Url} at {speed.ToString("0.0")}/s";
            }
            double? Percentage(ICopyProgress progress) => _options.DownloadExtractRatio * progress.PercentComplete;
            var archiveProgress = archiveProgressBar.AsProgress<ICopyProgress>(Message, Percentage);
            var archiveFileInfo = await DownloadArchiveAsync(download.Archive, archiveProgress, cancellationToken);

            archiveProgressBar.Message = $"Extracting {archiveFileInfo.FullName}";
            var extractProgress = archiveProgressBar.AsProgress<double>(percentage: f => _options.DownloadExtractRatio + (1 - _options.DownloadExtractRatio) * f);
            _extractor.ExtractArchive(download, archiveFileInfo, extractDirectory, extractProgress, cancellationToken);

            mainProgressBar.Tick();
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
            var downloads = SupportedPlatforms.Select(e => GetDownload(e, Product.CommunityServer, version, _options.PlatformIdentifiers[e], _options.Architecture, _options.Edition));
            return (version, downloads);
        }

        private async Task<(Version version, IEnumerable<Download> downloads)> GetDatabaseToolsDownloadsAsync(CancellationToken cancellationToken)
        {
            var release = await _options.HttpClient.GetFromJsonAsync<Release>(_options.DatabaseToolsUrl, cancellationToken) ?? throw new InvalidOperationException($"Failed to deserialize {nameof(Release)}");
            var version = release.Versions.FirstOrDefault() ?? throw new InvalidOperationException("No Database Tools version was found");
            var downloads = SupportedPlatforms.Select(e => GetDownload(e, Product.DatabaseTools, version, _options.PlatformIdentifiers[e], _options.Architecture));
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