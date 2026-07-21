using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Mongo2Go.Helper
{
    /// <summary>
    /// The SHA-256 checksums of the MongoDB binaries bundled with this build of Mongo2Go.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mongo2Go locates <c>mongod</c> by searching several directories and walking upwards from each of them, which
    /// means the search can reach directories that Mongo2Go does not control. Rather than trying to decide which
    /// <em>locations</em> are trustworthy, we verify the <em>contents</em>: a candidate directory is only used if the
    /// binaries in it are the ones shipped inside this package. Where the binaries were found then stops mattering.
    /// </para>
    /// <para>
    /// This applies to the default search only. When the caller explicitly supplies binaries - via the
    /// <c>binariesSearchDirectory</c> or <c>binariesSearchPatternOverride</c> parameters of
    /// <see cref="MongoDbRunner.Start(string,string,string,bool,string,ushort,Microsoft.Extensions.Logging.ILogger)"/> -
    /// they are used as-is. Supplying your own MongoDB build is a supported scenario (a newer server, or a native
    /// arm64 build) and those binaries will legitimately not match this manifest.
    /// </para>
    /// </remarks>
    internal static class MongoBinaryManifest
    {
        private const string ResourceName = "Mongo2Go.MongoBinaries.sha256";

        /// <summary>
        /// Accepted checksums for the current platform, keyed by architecture and then file name.
        /// </summary>
        /// <remarks>
        /// Architecture has to be part of the key because a platform can ship more than one - Linux ships x64 and
        /// arm64 - and unlike two copies of the same binary, these are not interchangeable. Running the wrong one
        /// fails with "Exec format error", which is the symptom reported in issue #127.
        /// </remarks>
        private static readonly Lazy<IDictionary<string, IDictionary<string, HashSet<string>>>> ExpectedChecksums =
            new Lazy<IDictionary<string, IDictionary<string, HashSet<string>>>>(LoadForCurrentPlatform);

        /// <summary>
        /// The manifest name for the architecture this machine runs natively.
        /// </summary>
        /// <remarks>
        /// <see cref="RuntimeInformation.OSArchitecture"/> rather than <c>ProcessArchitecture</c>: what matters is what
        /// the operating system can execute, not what this particular process was built for. A .NET process running
        /// under emulation should still prefer binaries native to the machine.
        /// </remarks>
        private static string NativeArchitecture
        {
            get
            {
                // A switch expression would be neater, but the net472 target compiles as C# 7.3.
                switch (RuntimeInformation.OSArchitecture)
                {
                    case Architecture.Arm64:
                        return "arm64";
                    case Architecture.X64:
                        return "x64";
                    default:
                        return RuntimeInformation.OSArchitecture.ToString().ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// Caches verification results per directory. Hashing the bundled binaries costs roughly 40 ms, which is
        /// negligible once per directory but not once per <see cref="MongoDbRunner"/> in a large test suite.
        /// </summary>
        private static readonly Dictionary<string, bool> VerificationCache = new Dictionary<string, bool>(StringComparer.Ordinal);

        private static readonly object CacheLock = new object();

        /// <summary>
        /// The executables that must be present and match for a directory to be accepted.
        /// </summary>
        internal static IEnumerable<string> ExpectedFileNames
        {
            get
            {
                var suffix = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty;
                yield return MongoDbDefaults.MongodExecutable + suffix;
                yield return MongoDbDefaults.MongoImportExecutable + suffix;
                yield return MongoDbDefaults.MongoExportExecutable + suffix;
            }
        }

        /// <summary>
        /// Returns <c>true</c> if <paramref name="binariesDirectory"/> contains binaries shipped with this build of
        /// Mongo2Go. A directory missing any of them, or containing a modified copy, is rejected.
        /// </summary>
        /// <param name="binariesDirectory">The candidate directory.</param>
        /// <param name="nativeArchitectureOnly">
        /// When <c>true</c>, only binaries built for this machine's architecture are accepted. Callers should search
        /// once with <c>true</c> and, if nothing matches, search again with <c>false</c>: running a binary of another
        /// architecture is correct on any platform that can emulate it, for which no native build is bundled. Since
        /// MongoDB 8.x this no longer includes Apple Silicon (a native macOS arm64 build now ships and matches the
        /// first pass); it is the safety net for any other such host.
        /// </param>
        public static bool Matches(string binariesDirectory, bool nativeArchitectureOnly)
        {
            if (string.IsNullOrEmpty(binariesDirectory))
            {
                return false;
            }

            var cacheKey = (nativeArchitectureOnly ? "native:" : "any:") + binariesDirectory;

            lock (CacheLock)
            {
                if (VerificationCache.TryGetValue(cacheKey, out var cached))
                {
                    return cached;
                }
            }

            var result = Verify(binariesDirectory, nativeArchitectureOnly);

            lock (CacheLock)
            {
                VerificationCache[cacheKey] = result;
            }

            return result;
        }

        private static bool Verify(string binariesDirectory, bool nativeArchitectureOnly)
        {
            // No manifest means this build cannot verify anything. Fail open rather than making the library unusable:
            // a missing manifest is a packaging fault on our side, not evidence that the user's binaries are bad.
            if (ExpectedChecksums.Value.Count == 0)
            {
                return true;
            }

            var architectures = nativeArchitectureOnly
                ? new[] { NativeArchitecture }
                : ExpectedChecksums.Value.Keys.ToArray();

            foreach (var fileName in ExpectedFileNames)
            {
                var accepted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var architecture in architectures)
                {
                    if (ExpectedChecksums.Value.TryGetValue(architecture, out var byFileName)
                        && byFileName.TryGetValue(fileName, out var checksums))
                    {
                        accepted.UnionWith(checksums);
                    }
                }

                if (accepted.Count == 0)
                {
                    return false;
                }

                var file = Path.Combine(binariesDirectory, fileName);
                if (!File.Exists(file))
                {
                    return false;
                }

                string actual;
                try
                {
                    actual = ComputeSha256(file);
                }
                catch (IOException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }

                if (!accepted.Contains(actual))
                {
                    return false;
                }
            }

            return true;
        }

        private static string ComputeSha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024))
            {
                var hash = sha.ComputeHash(stream);
                var builder = new System.Text.StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                {
                    builder.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }
                return builder.ToString();
            }
        }

        /// <summary>
        /// Reads the embedded manifest and returns the entries for the current platform, keyed by file name.
        /// </summary>
        private static IDictionary<string, IDictionary<string, HashSet<string>>> LoadForCurrentPlatform()
        {
            var result = new Dictionary<string, IDictionary<string, HashSet<string>>>(StringComparer.OrdinalIgnoreCase);

            string platform;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) platform = "windows";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) platform = "macos";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) platform = "linux";
            else return result;

            using (var stream = typeof(MongoBinaryManifest).GetTypeInfo().Assembly.GetManifestResourceStream(ResourceName))
            {
                if (stream == null)
                {
                    return result;
                }

                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var trimmed = line.Trim();
                        if (trimmed.Length == 0 || trimmed[0] == '#')
                        {
                            continue;
                        }

                        // "<platform>/<architecture>/<fileName>  <sha256>"
                        var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 2)
                        {
                            continue;
                        }

                        var key = parts[0].Split('/');
                        if (key.Length != 3)
                        {
                            continue;
                        }

                        if (!string.Equals(key[0], platform, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var architecture = key[1];
                        var fileName = key[2];

                        if (!result.TryGetValue(architecture, out var byFileName))
                        {
                            byFileName = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                            result[architecture] = byFileName;
                        }

                        if (!byFileName.TryGetValue(fileName, out var checksums))
                        {
                            checksums = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            byFileName[fileName] = checksums;
                        }

                        checksums.Add(parts[1]);
                    }
                }
            }

            return result;
        }
    }
}
