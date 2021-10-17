using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable CollectionNeverUpdated.Global

namespace MongoDownloader
{
    public enum Platform
    {
        Linux,
        // ReSharper disable once InconsistentNaming
        macOS,
        Windows,
    }

    public enum Product
    {
        CommunityServer,
        DatabaseTools,
    }

    /// <summary>
    /// The root object of the JSON describing the available releases.
    /// </summary>
    public class Release
    {
        [JsonPropertyName("versions")]
        public List<Version> Versions { get; set; } = new();
    }

    public class Version
    {
        [JsonPropertyName("version")]
        public string Number { get; set; } = "";

        [JsonPropertyName("production_release")]
        public bool Production { get; set; } = false;

        [JsonPropertyName("downloads")]
        public List<Download> Downloads { get; set; } = new();
    }

    public class Download
    {
        /// <summary>
        /// Used to identify the platform for the Community Server archives
        /// </summary>
        [JsonPropertyName("target")]
        public string Target { get; set; } = "";

        /// <summary>
        /// Used to identify the platform for the Database Tools archives
        /// </summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("arch")]
        public string Arch { get; set; } = "";

        [JsonPropertyName("edition")]
        public string Edition { get; set; } = "";

        [JsonPropertyName("archive")]
        public Archive Archive { get; set; } = new();

        public Product Product { get; set; }

        public Platform Platform { get; set; }

        public Architecture Architecture { get; set; }

        public override string ToString() => $"{Product} for {Platform}/{Architecture.ToString().ToLowerInvariant()}";
    }

    public class Archive
    {
        [JsonPropertyName("url")]
        public Uri Url { get; set; } = default!;
    }
}