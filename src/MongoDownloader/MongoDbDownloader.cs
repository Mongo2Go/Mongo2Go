using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http.Json;
using System.Security.Cryptography;
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
                var archiveProgress = context.AddTask($"Downloading {download} from {download.Archive.Url}", maxValue: initialMaxValue);
                var directoryName = $"mongodb-{download.Platform.ToString().ToLowerInvariant()}-{download.Architecture.ToString().ToLowerInvariant()}-{communityServerVersion.Number}-database-tools-{databaseToolsVersion.Number}";
                var extractDirectory = new DirectoryInfo(Path.Combine(toolsDirectory.FullName, directoryName));
                allArchiveProgresses.Add(archiveProgress);
                var progress = new ArchiveProgress(archiveProgress, globalProgress, allArchiveProgresses, download, $"✅ Downloaded and extracted MongoDB Community Server {communityServerVersion.Number} and Database Tools {databaseToolsVersion.Number} into {new Uri(toolsDirectory.FullName).AbsoluteUri}");
                tasks.Add(ProcessArchiveAsync(download, extractDirectory, progress, cancellationToken));
            }
            var strippedSizes = await Task.WhenAll(tasks);

            // Written here, after every archive has been extracted and stripped, so the manifest describes the
            // binaries exactly as they will be committed and packaged. Doing this automatically is the point: a
            // manifest that disagrees with tools/ makes Mongo2Go reject its own binaries at the consumer's end.
            var manifestProgress = context.AddTask("Writing checksum manifest", maxValue: 1);
            var manifestFile = await BinaryManifestWriter.WriteAsync(toolsDirectory, communityServerVersion.Number, databaseToolsVersion.Number, _extractor.StripToolVersion, cancellationToken);
            manifestProgress.Increment(1);
            manifestProgress.Description = $"✅ Wrote checksum manifest to {new Uri(manifestFile.FullName).AbsoluteUri}";

            return strippedSizes.Aggregate(new ByteSize(0), (current, strippedSize) => current + strippedSize);
        }

        private async Task<ByteSize> ProcessArchiveAsync(Download download, DirectoryInfo extractDirectory, ArchiveProgress progress, CancellationToken cancellationToken)
        {
            var archiveFileInfo = await DownloadArchiveAsync(download.Archive, progress, cancellationToken);

            progress.Report("Verifying checksum");
            VerifyChecksum(archiveFileInfo, download);

            var stripTasks = await _extractor.ExtractArchiveAsync(download, archiveFileInfo, extractDirectory, cancellationToken);
            progress.Report("Stripping binaries");
            var completedStripTasks = await Task.WhenAll(stripTasks);
            var totalStrippedSize = completedStripTasks.Aggregate(new ByteSize(0), (current, strippedSize) => current + strippedSize);
            progress.ReportCompleted(totalStrippedSize);
            return totalStrippedSize;
        }

        /// <summary>
        /// Verifies a downloaded archive against the SHA-256 checksum published by MongoDB alongside its download URL.
        /// </summary>
        /// <remarks>
        /// The binaries committed to <c>tools/</c> are stripped derivatives and therefore cannot themselves be checked
        /// against anything MongoDB publishes. This is the only point in the chain where upstream provenance can be
        /// established, so a mismatch is fatal rather than a warning: continuing would produce binaries that are then
        /// committed, packaged, and executed on every consumer's machine.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The checksum is missing or does not match.</exception>
        private static void VerifyChecksum(FileInfo archiveFile, Download download)
        {
            var expected = download.Archive.Sha256;
            if (string.IsNullOrWhiteSpace(expected))
            {
                throw new InvalidOperationException(
                    $"MongoDB published no SHA-256 checksum for {download} ({download.Archive.Url}). " +
                    $"Refusing to use an archive whose integrity cannot be established.");
            }

            string actual;
            using (var sha256 = SHA256.Create())
            using (var stream = archiveFile.OpenRead())
            {
                actual = Convert.ToHexString(sha256.ComputeHash(stream));
            }

            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                // Leaving the file in place would let a cached copy be reused on the next run.
                archiveFile.Delete();
                throw new InvalidOperationException(
                    $"Checksum mismatch for {download} downloaded from {download.Archive.Url}.{Environment.NewLine}" +
                    $"  expected (published by MongoDB): {expected.ToLowerInvariant()}{Environment.NewLine}" +
                    $"  actual   (what we received):     {actual.ToLowerInvariant()}{Environment.NewLine}" +
                    $"The downloaded file has been deleted. Do not use these binaries.");
            }
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
            // FileMode.Create rather than OpenWrite: OpenWrite does not truncate, so a shorter download would be
            // left with trailing bytes from a longer previous one, and the checksum would fail for a confusing reason.
            await using var destinationStream = new FileStream(destinationFile.FullName, FileMode.Create, FileAccess.Write, FileShare.None);
            await _options.HttpClient.GetAsync(archive.Url.AbsoluteUri, destinationStream, progress, cancellationToken);
            destinationFile.Refresh();
            return destinationFile;
        }

        private async Task<(Version version, IEnumerable<Download> downloads)> GetCommunityServerDownloadsAsync(CancellationToken cancellationToken)
        {
            var pinned = _options.CommunityServerVersion;
            // The current-releases feed only lists recent versions, so pinning an older one requires the full feed.
            var url = string.IsNullOrEmpty(pinned) ? _options.CommunityServerUrl : _options.CommunityServerFullUrl;
            Func<Version, bool> predicate = string.IsNullOrEmpty(pinned)
                ? version => version.Production
                : version => version.Number == pinned;

            // Streamed rather than buffered: the full feed is ~50 MB and we only need one version.
            await using var stream = await _options.HttpClient.GetStreamAsync(url, cancellationToken);
            var selected = await MongoReleaseReader.FindVersionAsync(stream, predicate, cancellationToken)
                ?? throw new InvalidOperationException(string.IsNullOrEmpty(pinned)
                    ? $"No Community Server production version was found in {url}"
                    : $"Community Server version \"{pinned}\" was not found in {url}");

            var downloads = Enum.GetValues<Platform>().SelectMany(platform => GetDownloads(platform, Product.CommunityServer, selected, _options, _options.Edition));
            return (selected, downloads);
        }

        private async Task<(Version version, IEnumerable<Download> downloads)> GetDatabaseToolsDownloadsAsync(CancellationToken cancellationToken)
        {
            var pinned = _options.DatabaseToolsVersion;
            var url = string.IsNullOrEmpty(pinned) ? _options.DatabaseToolsUrl : _options.DatabaseToolsFullUrl;
            // With no version pinned, the first version in the feed is the newest, matching the previous behaviour.
            Func<Version, bool> predicate = string.IsNullOrEmpty(pinned)
                ? _ => true
                : version => version.Number == pinned;

            await using var stream = await _options.HttpClient.GetStreamAsync(url, cancellationToken);
            var selected = await MongoReleaseReader.FindVersionAsync(stream, predicate, cancellationToken)
                ?? throw new InvalidOperationException(string.IsNullOrEmpty(pinned)
                    ? $"No Database Tools version was found in {url}"
                    : $"Database Tools version \"{pinned}\" was not found in {url}");

            var downloads = Enum.GetValues<Platform>().SelectMany(platform => GetDownloads(platform, Product.DatabaseTools, selected, _options));
            return (selected, downloads);
        }

        private static IEnumerable<Download> GetDownloads(Platform platform, Product product, Version version, Options options, Regex? editionRegex = null)
        {
            var platformRegex = options.PlatformIdentifiers[platform];
            Func<Download, bool> platformPredicate = product switch
            {
                Product.CommunityServer => download => platformRegex.IsMatch(download.Target),
                Product.DatabaseTools => download => platformRegex.IsMatch(download.Name),
                _ => throw new ArgumentOutOfRangeException(nameof(product), product, $"The value of argument '{nameof(product)}' ({product}) is invalid for enum type '{nameof(Product)}'.")
            };

            foreach (var architecture in options.Architectures[platform])
            {
                var architectureRegex = options.ArchitectureIdentifiers[architecture];
                var matchingDownloads = version.Downloads
                    .Where(platformPredicate)
                    .Where(e => architectureRegex.IsMatch(e.Arch))
                    .Where(e => editionRegex?.IsMatch(e.Edition) ?? true)
                    .ToList();

                if (matchingDownloads.Count == 0)
                {
                    var downloads = version.Downloads.OrderBy(e => e.Target).ThenBy(e => e.Arch);
                    var messages = Enumerable.Empty<string>()
                        .Append($"Download not found for {platform}/{architecture}.")
                        .Append($"  Available downloads for {product} {version.Number}:")
                        .Concat(downloads.Select(e => $"    - {e.Target}/{e.Arch} ({e.Edition})"));
                    throw new InvalidOperationException(string.Join(Environment.NewLine, messages));
                }

                if (matchingDownloads.Count > 1)
                {
                    throw new InvalidOperationException($"Found {matchingDownloads.Count} downloads for {platform}/{architecture} but expected to find only one.");
                }

                var download = matchingDownloads[0];
                download.Platform = platform;
                download.Architecture = architecture;
                download.Product = product;

                yield return download;
            }
        }
    }
}