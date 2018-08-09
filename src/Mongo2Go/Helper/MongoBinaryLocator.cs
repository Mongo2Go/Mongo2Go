using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Mongo2Go.Helper
{

    public class MongoBinaryLocator : IMongoBinaryLocator
    {
        private readonly string _nugetPrefix = Path.Combine("packages", "Mongo2Go*");
        private readonly string _nugetCachePrefix = Path.Combine("packages", "mongo2go", "*");
        public const string DefaultWindowsSearchPattern = @"tools\mongodb-win32*\bin";
        public const string DefaultLinuxSearchPattern = "tools/mongodb-linux*/bin";
        public const string DefaultOsxSearchPattern = "tools/mongodb-osx*/bin";
        public const string WindowsNugetCacheLocation = @"%USERPROFILE%\.nuget\packages";
        public const string OsxAndLinuxNugetCacheLocation = "~/.nuget/packages";
        private string _binFolder = string.Empty;
        private readonly string _searchPattern;
        private readonly string _nugetCacheLocation;

        public MongoBinaryLocator (string searchPatternOverride)
        {
            if (string.IsNullOrEmpty(searchPatternOverride))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    _searchPattern = DefaultOsxSearchPattern;
                    _nugetCacheLocation = OsxAndLinuxNugetCacheLocation;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    _searchPattern = DefaultLinuxSearchPattern;
                    _nugetCacheLocation = OsxAndLinuxNugetCacheLocation;
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _searchPattern = DefaultWindowsSearchPattern;
                    _nugetCacheLocation = Environment.ExpandEnvironmentVariables(WindowsNugetCacheLocation);
                }
                else
                {
                    throw new MonogDbBinariesNotFoundException($"Unknown OS: {RuntimeInformation.OSDescription}");
                }
            }
            else
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

        private string ResolveBinariesDirectory ()
        {
            var binariesFolder =
                // First search from the project directory
                GetBinariesFolder(FolderSearch.CurrentExecutingDirectory()) ??
                // Second search from the nuget cache location
                GetBinariesFolder(_nugetCacheLocation);

            if (binariesFolder == null) {
                throw new MonogDbBinariesNotFoundException (
                    $"Could not find Mongo binaries using the search patterns \"{Path.Combine(_nugetPrefix, _searchPattern)}\", \"{Path.Combine(_nugetCachePrefix, _searchPattern)}\", and \"{_searchPattern}\".  " +
                    $"You can override the search pattern when calling MongoDbRunner.Start.  We have detected the OS as {RuntimeInformation.OSDescription}.\n" +
                    $"We walked up to root directory from the following locations.\n{FolderSearch.CurrentExecutingDirectory()}\n{_nugetCacheLocation}");
            }
            return binariesFolder;
        }

        private string GetBinariesFolder(string startFromPath)
        {
            return
                // First try path when installed via nuget    
                startFromPath.FindFolderUpwards(Path.Combine(_nugetPrefix, _searchPattern)) ??
                // Second try path when started from solution
                startFromPath.FindFolderUpwards(_searchPattern) ??
                //Third try with the nuget cache prefix
                startFromPath.FindFolderUpwards(Path.Combine(_nugetCachePrefix, _searchPattern));
        }
    }
}