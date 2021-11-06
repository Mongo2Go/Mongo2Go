using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Mongo2Go.Helper
{

    public class MongoBinaryLocator : IMongoBinaryLocator
    {
        private readonly string _nugetPrefix = Path.Combine("packages", "Mongo2Go*");
        private readonly string _nugetCachePrefix = Path.Combine("packages", "mongo2go", "*");
        private readonly string _nugetCacheBasePrefix = Path.Combine("mongo2go", "*");
        public const string DefaultWindowsSearchPattern = @"tools\mongodb-windows*\bin";
        public const string DefaultLinuxSearchPattern = "*/tools/mongodb-linux*/bin";
        public const string DefaultOsxSearchPattern = "tools/mongodb-macos*/bin";
        public const string WindowsNugetCacheLocation = @"%USERPROFILE%\.nuget\packages";
        public static readonly string OsxAndLinuxNugetCacheLocation = Environment.GetEnvironmentVariable("HOME") + "/.nuget/packages/mongo2go";
        private string _binFolder = string.Empty;
        private readonly string _searchPattern;
        private readonly string _nugetCacheDirectory;
        private readonly string _additionalSearchDirectory;

        public MongoBinaryLocator(string searchPatternOverride, string additionalSearchDirectory)
        {
            _additionalSearchDirectory = additionalSearchDirectory;
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
            foreach (var directory in searchDirectories)
            {
                var binaryFolder =
                    // First try just the search pattern
                    directory.FindFolderUpwards(_searchPattern) ??
                    // Next try the search pattern with nuget installation prefix
                    directory.FindFolderUpwards(Path.Combine(_nugetPrefix, _searchPattern)) ??
                    // Finally try the search pattern with the nuget cache prefix
                    directory.FindFolderUpwards(Path.Combine(_nugetCachePrefix, _searchPattern)) ??
                    // Finally try the search pattern with the basic nuget cache prefix
                    directory.FindFolderUpwards(Path.Combine(_nugetCacheBasePrefix, _searchPattern));
                if (binaryFolder != null) return binaryFolder;
            }
            throw new MonogDbBinariesNotFoundException(
                $"Could not find Mongo binaries using the search patterns \"{_searchPattern}\", \"{Path.Combine(_nugetPrefix, _searchPattern)}\", and \"{Path.Combine(_nugetCachePrefix, _searchPattern)}\".  " +
                $"You can override the search pattern and directory when calling MongoDbRunner.Start.  We have detected the OS as {RuntimeInformation.OSDescription}.\n" +
                $"We walked up to root directory from the following locations.\n {string.Join("\n", searchDirectories)}");
        }
    }
}
