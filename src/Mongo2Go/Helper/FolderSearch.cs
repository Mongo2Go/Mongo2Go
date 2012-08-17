using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Mongo2Go.Helper
{
    public static class FolderSearch
    {
        private const int MaxLevelOfRecursion = 6;

        public static string CurrentExecutingDirectory()
        {
            string filePath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            return Path.GetDirectoryName(filePath);
        }

        public static string FindFolder(this string startPath, string searchPattern)
        {
            string currentPath = startPath;

            foreach (var part in searchPattern.Split(new[] { @"\" }, StringSplitOptions.None))
            {
                string[] matchesDirectory = Directory.GetDirectories(currentPath, part);
                if (!matchesDirectory.Any())
                {
                    return null;
                }
                currentPath = matchesDirectory.OrderBy(x => x).Last();
            }

            return currentPath;
        }

        public static string FindFolderUpwards(this string startPath, string searchPattern)
        {
            return FindFolderUpwards(startPath, searchPattern, 0);
        }
        
        private static string FindFolderUpwards(this string startPath, string searchPattern, int currentLevel)
        {
            if (startPath == null)
            {
                return null;
            }

            if (currentLevel >= MaxLevelOfRecursion)
            {
                return null;
            }

            string matchingFolder = startPath.FindFolder(searchPattern);
            return matchingFolder ?? startPath.RemoveLastPart().FindFolderUpwards(searchPattern, currentLevel + 1);
        }

        internal static string RemoveLastPart(this string path)
        {
            if (!path.Contains(@"\"))
            {
                return null;
            }

            List<string> parts = path.Split(new[] {@"\"}, StringSplitOptions.None).ToList();
            parts.RemoveAt(parts.Count() - 1);
            return string.Join(@"\", parts.ToArray());
        }

        /// <summary>
        /// Absolute path stays unchanged, relative path will be relative to current executing directory (usually the /bin folder)
        /// </summary>
        public static string FinalizePath(string fileName)
        {
            string finalPath;

            if (Path.IsPathRooted(fileName))
            {
                finalPath = fileName;
            }
            else
            {
                finalPath = CurrentExecutingDirectory() + @"\" + fileName;
                finalPath = Path.GetFullPath(finalPath);
            }

            return finalPath;
        }
    }
}
