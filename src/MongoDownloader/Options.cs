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
        /// The exact MongoDB Community Server version to download, e.g. <c>8.0.26</c>. When <c>null</c> or empty, the
        /// latest production release is used.
        /// </summary>
        /// <remarks>
        /// Server and Database Tools have independent version numbers (for example 8.0.26 and 100.14.0), so they are
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
            [Platform.macOS] = new[] { Architecture.Arm64, Architecture.X64 },
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
            // ubuntu2204, not ubuntu2004: the Ubuntu 20.04 build of MongoDB 8.x links OpenSSL 1.1 (libssl.so.1.1),
            // which is absent on modern distributions and is exactly the "cannot open shared object file
            // libcrypto.so.1.1 / libssl.so.1.1" failure this upgrade set out to fix (#149, #135). The 22.04 build
            // links OpenSSL 3, which ships on every currently supported distribution. The floor this sets is glibc
            // 2.35 (Ubuntu 22.04, Debian 12, RHEL 9, Amazon Linux 2023).
            [Platform.Linux] = new(@"ubuntu2204", RegexOptions.IgnoreCase),
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
            // The vendored tree ships only the three executables per platform (mongod, mongoimport, mongoexport) and
            // nothing else - no per-platform LICENSE/README/THIRD-PARTY-NOTICES copies. Those upstream notices are
            // instead carried once at the tools/ root, which keeps five near-identical copies of MongoDB's ~0.4 MB
            // THIRD-PARTY-NOTICES out of a package that is right against the 250 MiB nuget.org limit. NeverMatches
            // captures nothing, so ExtractArchiveAsync keeps only the binaries.
            [(Product.CommunityServer, Platform.Linux)]   = NeverMatches,
            [(Product.CommunityServer, Platform.macOS)]   = NeverMatches,
            [(Product.CommunityServer, Platform.Windows)] = NeverMatches,
            [(Product.DatabaseTools,   Platform.Linux)]   = NeverMatches,
            [(Product.DatabaseTools,   Platform.macOS)]   = NeverMatches,
            [(Product.DatabaseTools,   Platform.Windows)] = NeverMatches,
        };

        // An empty negative look-ahead can never succeed, so this matches no entry at all.
        private static readonly Regex NeverMatches = new(@"(?!)");
    }
}