using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
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
        /// The URL of the MongoDB Community Server download information JSON.
        /// </summary>
        public string CommunityServerUrl { get; init; } = "https://s3.amazonaws.com/downloads.mongodb.org/current.json";

        /// <summary>
        /// The URL of the MongoDB Database Tools download information JSON.
        /// </summary>
        public string DatabaseToolsUrl { get; init; } = "https://s3.amazonaws.com/downloads.mongodb.org/tools/db/release.json";

        /// <summary>
        /// The directory to store the downloaded archive files.
        /// </summary>
        public DirectoryInfo CacheDirectory { get; init; } = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.InternetCache), nameof(MongoDownloader)));

        /// <summary>
        /// The architecture of the archive to download.
        /// </summary>
        public Regex Architecture { get; init; } = new("x86_64");

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