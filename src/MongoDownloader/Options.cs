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
        /// The estimated ratio between download time and extraction time for the progress bar.
        /// </summary>
        public double DownloadExtractRatio { get; init; } = 0.99;
    }
}