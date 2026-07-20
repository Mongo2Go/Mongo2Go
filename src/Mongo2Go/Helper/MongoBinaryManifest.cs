using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
        /// Accepted checksums for the current platform, keyed by file name.
        /// </summary>
        /// <remarks>
        /// A file name maps to a <em>set</em> of checksums rather than one, because a platform can ship more than one
        /// architecture - Linux ships x64 and arm64 - and both are legitimately ours. The question being answered is
        /// "is this one of the binaries we shipped?", not "is this the single binary we expected here?".
        /// </remarks>
        private static readonly Lazy<IDictionary<string, HashSet<string>>> ExpectedChecksums =
            new Lazy<IDictionary<string, HashSet<string>>>(LoadForCurrentPlatform);

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
        /// Returns <c>true</c> if <paramref name="binariesDirectory"/> contains exactly the binaries shipped with this
        /// build of Mongo2Go. A directory missing any of them, or containing a modified copy, is rejected.
        /// </summary>
        public static bool Matches(string binariesDirectory)
        {
            if (string.IsNullOrEmpty(binariesDirectory))
            {
                return false;
            }

            lock (CacheLock)
            {
                if (VerificationCache.TryGetValue(binariesDirectory, out var cached))
                {
                    return cached;
                }
            }

            var result = Verify(binariesDirectory);

            lock (CacheLock)
            {
                VerificationCache[binariesDirectory] = result;
            }

            return result;
        }

        private static bool Verify(string binariesDirectory)
        {
            // No manifest means this build cannot verify anything. Fail open rather than making the library unusable:
            // a missing manifest is a packaging fault on our side, not evidence that the user's binaries are bad.
            if (ExpectedChecksums.Value.Count == 0)
            {
                return true;
            }

            foreach (var fileName in ExpectedFileNames)
            {
                if (!ExpectedChecksums.Value.TryGetValue(fileName, out var accepted) || accepted.Count == 0)
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
        private static IDictionary<string, HashSet<string>> LoadForCurrentPlatform()
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

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

                        // "<platform>/<fileName>  <sha256>"
                        var parts = trimmed.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 2)
                        {
                            continue;
                        }

                        var separator = parts[0].IndexOf('/');
                        if (separator <= 0)
                        {
                            continue;
                        }

                        if (!string.Equals(parts[0].Substring(0, separator), platform, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var fileName = parts[0].Substring(separator + 1);
                        if (!result.TryGetValue(fileName, out var checksums))
                        {
                            checksums = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                            result[fileName] = checksums;
                        }
                        checksums.Add(parts[1]);
                    }
                }
            }

            return result;
        }
    }
}
