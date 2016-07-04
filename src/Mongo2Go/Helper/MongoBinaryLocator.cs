using System;

namespace Mongo2Go.Helper
{

    public class MongoBinaryLocator : IMongoBinaryLocator
    {
        private string nugetPrefix = System.IO.Path.Combine ("packages", "Mongo2Go*");
        private string searchPattern = "";

        public MongoBinaryLocator (string binSearchPattern)
        {
            if (string.IsNullOrEmpty (binSearchPattern)) {
                throw new ArgumentException ("Missing the search pattern for finding the mongoDb binaries.", "binSearchPattern");
            }

            searchPattern = binSearchPattern;
        }

        public string ResolveBinariesDirectory ()
        {
            // 1st: path when installed via nuget
            // 2nd: path when started from solution
            string binariesFolder = FolderSearch.CurrentExecutingDirectory ().FindFolderUpwards (System.IO.Path.Combine (nugetPrefix, searchPattern)) ??
                                    FolderSearch.CurrentExecutingDirectory ().FindFolderUpwards (searchPattern);

            if (binariesFolder == null) {
                throw new MonogDbBinariesNotFoundException (string.Format (
                    "Could not find Mongo binaries using {0}. We walk up the directories {1} levels from {2}",
                    searchPattern,
                    FolderSearch.MaxLevelOfRecursion,
                    FolderSearch.CurrentExecutingDirectory ()));
            }
            return binariesFolder;
        }
    }
}