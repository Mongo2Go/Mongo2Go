using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace MongoDownloader
{
    internal class Options
    {
        /// <summary>
        /// The <see cref="HttpClient"/> instance used to fetch data over HTTP.
        /// </summary>
        public HttpClient HttpClient { get; init; } = new();

        /// <summary>
        /// The URL of the MongoDB Community Server download information JSON, listing the current production releases.
        /// </summary>
        public string CommunityServerUrl { get; init; } = "https://s3.amazonaws.com/downloads.mongodb.org/current.json";

        /// <summary>
        /// The URL of the MongoDB Database Tools download information JSON, listing the current releases.
        /// </summary>
        public string DatabaseToolsUrl { get; init; } = "https://s3.amazonaws.com/downloads.mongodb.org/tools/db/release.json";

        /// <summary>
        /// The URL of the MongoDB Community Server download information JSON listing <em>every</em> release, used when a
        /// specific <see cref="CommunityServerVersion"/> is pinned because the current-releases feed only lists recent
        /// versions.
        /// </summary>
        public string CommunityServerFullUrl { get; init; } = "https://s3.amazonaws.com/downloads.mongodb.org/full.json";

        /// <summary>
        /// The URL of the MongoDB Database Tools download information JSON listing <em>every</em> release, used when a
        /// specific <see cref="DatabaseToolsVersion"/> is pinned.
        /// </summary>
        public string DatabaseToolsFullUrl { get; init; } = "https://s3.amazonaws.com/downloads.mongodb.org/tools/db/full.json";

        /// <summary>
        /// The exact MongoDB Community Server version to download, e.g. <c>4.4.4</c>. When <c>null</c> or empty, the
        /// latest production release is used.
        /// </summary>
        /// <remarks>
        /// Server and Database Tools have independent version numbers (for example 4.4.4 and 100.3.1), so they are
        /// pinned separately. Pinning is what lets a maintainer re-download an exact past version and confirm the
        /// bundled binaries reproduce byte-for-byte.
        /// </remarks>
        public string? CommunityServerVersion { get; init; }

        /// <summary>
        /// The exact MongoDB Database Tools version to download, e.g. <c>100.3.1</c>. When <c>null</c> or empty, the
        /// latest release is used.
        /// </summary>
        public string? DatabaseToolsVersion { get; init; }

        /// <summary>
        /// The directory to store the downloaded archive files.
        /// </summary>
        public DirectoryInfo CacheDirectory { get; init; } = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.InternetCache), nameof(MongoDownloader)));

        /// <summary>
        /// The architectures to download for a given platform.
        /// </summary>
        public IReadOnlyDictionary<Platform, IReadOnlyCollection<Architecture>> Architectures { get; init; } = new Dictionary<Platform, IReadOnlyCollection<Architecture>>
        {
            [Platform.Linux] = new[] { Architecture.Arm64, Architecture.X64 },
            [Platform.macOS] = new[] { Architecture.X64 },
            [Platform.Windows] = new[] { Architecture.X64 },
        };

        /// <summary>
        /// The edition of the archive to download.
        /// </summary>
        /// <remarks>macOS and Windows use <c>base</c> and Linux uses <c>targeted</c> for the community edition</remarks>
        public Regex Edition { get; init; } = new(@"base|targeted");

        /// <summary>
        /// The regular expressions used to identify platform-specific archives to download.
        /// </summary>
        public IReadOnlyDictionary<Platform, Regex> PlatformIdentifiers { get; init; } = new Dictionary<Platform, Regex>
        {
            [Platform.Linux] = new(@"ubuntu2004", RegexOptions.IgnoreCase),
            [Platform.macOS] = new(@"macOS", RegexOptions.IgnoreCase),
            [Platform.Windows] = new(@"windows", RegexOptions.IgnoreCase),
        };

        /// <summary>
        /// The regular expressions used to identify architectures to download.
        /// </summary>
        public IReadOnlyDictionary<Architecture, Regex> ArchitectureIdentifiers { get; init; } = new Dictionary<Architecture, Regex>
        {
            [Architecture.Arm64] = new("arm64|aarch64", RegexOptions.IgnoreCase),
            [Architecture.X64] = new("x86_64", RegexOptions.IgnoreCase),
        };

        /// <summary>
        /// A dictionary describing how to match MongoDB binaries inside the zip archives.
        /// <para/>
        /// The key is a tuple with the <see cref="Product"/>/<see cref="Platform"/> and the
        /// value is a regular expressions to match against the zip file name entry.
        /// </summary>
        public IReadOnlyDictionary<(Product, Platform), Regex> Binaries { get; init; } = new Dictionary<(Product, Platform), Regex>
        {
            [(Product.CommunityServer, Platform.Linux)]   = new(@"bin/mongod"),
            [(Product.CommunityServer, Platform.macOS)]   = new(@"bin/mongod"),
            [(Product.CommunityServer, Platform.Windows)] = new(@"bin/mongod\.exe"),
            [(Product.DatabaseTools,   Platform.Linux)]   = new(@"bin/(mongoexport|mongoimport)"),
            [(Product.DatabaseTools,   Platform.macOS)]   = new(@"bin/(mongoexport|mongoimport)"),
            [(Product.DatabaseTools,   Platform.Windows)] = new(@"bin/(mongoexport|mongoimport)\.exe"),
        };

        /// <summary>
        /// A dictionary describing how to match licence files inside the zip archives.
        /// <para/>
        /// The key is a tuple with the <see cref="Product"/>/<see cref="Platform"/> and the
        /// value is a regular expressions to match against the zip file name entry.
        /// </summary>
        public IReadOnlyDictionary<(Product, Platform), Regex> Licenses { get; init; } = new Dictionary<(Product, Platform), Regex>
        {
            // The regular expression matches anything at the zip top level, i.e. does not contain any slash (/) character
            [(Product.CommunityServer, Platform.Linux)]   = new(@"^[^/]+$"),
            [(Product.CommunityServer, Platform.macOS)]   = new(@"^[^/]+$"),
            [(Product.CommunityServer, Platform.Windows)] = new(@"^[^/]+$"),
            [(Product.DatabaseTools,   Platform.Linux)]   = new(@"^[^/]+$"),
            [(Product.DatabaseTools,   Platform.macOS)]   = new(@"^[^/]+$"),
            [(Product.DatabaseTools,   Platform.Windows)] = new(@"^[^/]+$"),
        };
    }
}