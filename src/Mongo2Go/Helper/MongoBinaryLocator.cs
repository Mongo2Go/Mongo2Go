using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace Mongo2Go.Helper
{

    public class MongoBinaryLocator : IMongoBinaryLocator
    {
        private readonly string _nugetPrefix = Path.Combine("packages", "Mongo2Go*");
        private readonly string _nugetCachePrefix = Path.Combine("packages", "mongo2go", "*");
        private readonly string _nugetCacheBasePrefix = Path.Combine("mongo2go", "*");
        public const string DefaultWindowsSearchPattern = @"tools\mongodb-windows*\bin";
        public const string DefaultLinuxSearchPattern = "tools/mongodb-linux*/bin";
        public const string DefaultOsxSearchPattern = "tools/mongodb-macos*/bin";
        public const string WindowsNugetCacheLocation = @"%USERPROFILE%\.nuget\packages";
        public static readonly string OsxAndLinuxNugetCacheLocation = Environment.GetEnvironmentVariable("HOME") + "/.nuget/packages";
        private string _binFolder = string.Empty;
        private readonly string _searchPattern;
        private readonly string _nugetCacheDirectory;
        private readonly string _additionalSearchDirectory;

        /// <summary>
        /// Whether binaries found by the search must match the checksums bundled with this build.
        /// </summary>
        /// <remarks>
        /// The search walks upwards from several starting points and can therefore reach directories Mongo2Go does not
        /// control, so by default we only accept binaries whose contents are the ones we shipped. A caller who names a
        /// search directory or overrides the search pattern is deliberately supplying their own MongoDB - a supported
        /// scenario for newer servers or native arm64 builds - and those binaries are used as they are.
        /// </remarks>
        private readonly bool _verifyChecksums;

        private readonly ILogger _logger;

        public MongoBinaryLocator(string searchPatternOverride, string additionalSearchDirectory)
            : this(searchPatternOverride, additionalSearchDirectory, null)
        {
        }

        public MongoBinaryLocator(string searchPatternOverride, string additionalSearchDirectory, ILogger logger)
        {
            _logger = logger;
            _additionalSearchDirectory = additionalSearchDirectory;
            _verifyChecksums = string.IsNullOrEmpty(searchPatternOverride) && string.IsNullOrEmpty(additionalSearchDirectory);
            _nugetCacheDirectory = Environment.GetEnvironmentVariable("NUGET_PACKAGES");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _searchPattern = DefaultOsxSearchPattern;
                _nugetCacheDirectory = _nugetCacheDirectory ?? OsxAndLinuxNugetCacheLocation;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _searchPattern = DefaultLinuxSearchPattern;
                _nugetCacheDirectory = _nugetCacheDirectory ?? OsxAndLinuxNugetCacheLocation;
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                _searchPattern = DefaultWindowsSearchPattern;
                _nugetCacheDirectory = _nugetCacheDirectory ?? Environment.ExpandEnvironmentVariables(WindowsNugetCacheLocation);
            }
            else
            {
                throw new MonogDbBinariesNotFoundException($"Unknown OS: {RuntimeInformation.OSDescription}");
            }

            if (!string.IsNullOrEmpty(searchPatternOverride))
            {
                _searchPattern = searchPatternOverride;
            }
        }

        public string Directory {
            get {
                if (string.IsNullOrEmpty(_binFolder)){
                    return _binFolder = ResolveBinariesDirectory ();
                } else {
                    return _binFolder;
                }
            }
        }

        private string ResolveBinariesDirectory()
        {
            var searchDirectories = new[]
            {
                // First search from the additional search directory, if provided
                _additionalSearchDirectory,
                // Then search from the project directory
                FolderSearch.CurrentExecutingDirectory(),
                // Finally search from the nuget cache directory
                _nugetCacheDirectory
            };
            return FindBinariesDirectory(searchDirectories.Where(x => !string.IsNullOrWhiteSpace(x)).ToList());
        }

        private string FindBinariesDirectory(IList<string> searchDirectories)
        {
            var patterns = new[]
            {
                // First try just the search pattern
                _searchPattern,
                // Next try the search pattern with nuget installation prefix
                Path.Combine(_nugetPrefix, _searchPattern),
                // Then try the search pattern with the nuget cache prefix
                Path.Combine(_nugetCachePrefix, _searchPattern),
                // Finally try the search pattern with the basic nuget cache prefix
                Path.Combine(_nugetCacheBasePrefix, _searchPattern)
            };

            var rejected = new List<string>();

            foreach (var directory in searchDirectories)
            {
                foreach (var pattern in patterns)
                {
                    foreach (var candidate in directory.FindFoldersUpwards(pattern))
                    {
                        if (!_verifyChecksums || MongoBinaryManifest.Matches(candidate))
                        {
                            return candidate;
                        }

                        // Keep searching: a directory that merely looks right must not mask the real one.
                        // Recovering from this is deliberately not silent - a directory that matches the search
                        // pattern but holds different binaries is worth knowing about whether it is a stale copy
                        // or a planted one, and the checksum cannot be forged, so there is nothing to keep quiet.
                        if (!rejected.Contains(candidate))
                        {
                            rejected.Add(candidate);
                            _logger?.LogWarning(
                                "Ignoring MongoDB binaries at \"{BinariesDirectory}\": they match the search pattern " +
                                "but are not the binaries shipped with this version of Mongo2Go. Continuing to search. " +
                                "If these are your own binaries, pass the directory to MongoDbRunner.Start using the " +
                                "binariesSearchDirectory parameter and it will be used without this check.",
                                candidate);
                        }
                    }
                }
            }

            var message =
                $"Could not find Mongo binaries using the search patterns \"{_searchPattern}\", \"{Path.Combine(_nugetPrefix, _searchPattern)}\", \"{Path.Combine(_nugetCachePrefix, _searchPattern)}\", and \"{Path.Combine(_nugetCacheBasePrefix, _searchPattern)}\".  " +
                $"You can override the search pattern and directory when calling MongoDbRunner.Start.  We have detected the OS as {RuntimeInformation.OSDescription}.\n" +
                $"We walked up to root directory from the following locations.\n {string.Join("\n", searchDirectories)}";

            if (rejected.Count > 0)
            {
                message +=
                    $"\n\nThe following directories matched the search pattern but do not contain the MongoDB binaries " +
                    $"shipped with this version of Mongo2Go, so they were skipped:\n {string.Join("\n ", rejected)}\n" +
                    $"If you are deliberately using your own MongoDB build, pass it with the binariesSearchDirectory " +
                    $"parameter of MongoDbRunner.Start and it will be used without this check.";
            }

            throw new MonogDbBinariesNotFoundException(message);
        }
    }
}
