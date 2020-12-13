using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using MongoDB.Bson.Serialization.Serializers;

namespace Mongo2Go.Helper
{
    public static class FolderSearch
    {
        private static readonly char[] _separators = { Path.DirectorySeparatorChar };

        public static string CurrentExecutingDirectory()
        {
            string filePath = new Uri(typeof(FolderSearch).GetTypeInfo().Assembly.CodeBase).LocalPath;
            return Path.GetDirectoryName(filePath);
        }

        public static string FindFolder(this string startPath, string searchPattern)
        {
            if (startPath == null || searchPattern == null)
            {
                return null;
            }

            string currentPath = startPath;

            foreach (var part in searchPattern.Split(_separators, StringSplitOptions.None))
            {
                if (!Directory.Exists(currentPath))
                {
                    return null;
                }

                string[] matchesDirectory = Directory.GetDirectories(currentPath, part);
                if (!matchesDirectory.Any())
                {
                    return null;
                }

                if (matchesDirectory.Length > 1)
                {
                    currentPath = MatchVersionToAssemblyVersion(matchesDirectory)
                        ?? matchesDirectory.OrderBy(x => x).Last();
                }
                else
                {
                    currentPath = matchesDirectory.First();
                }
            }

            return currentPath;
        }

        public static string FindFolderUpwards(this string startPath, string searchPattern)
        {
            if (string.IsNullOrEmpty(startPath))
            {
                return null;
            }

            string matchingFolder = startPath.FindFolder(searchPattern);
            return matchingFolder ?? startPath.RemoveLastPart().FindFolderUpwards(searchPattern);
        }

        internal static string RemoveLastPart(this string path)
        {
            if (!path.Contains(Path.DirectorySeparatorChar))
            {
                return null;
            }

            List<string> parts = path.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.None).ToList();
            parts.RemoveAt(parts.Count() - 1);
            return string.Join(Path.DirectorySeparatorChar.ToString(), parts.ToArray());
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
                finalPath = Path.Combine(CurrentExecutingDirectory(), fileName);
                finalPath = Path.GetFullPath(finalPath);
            }

            return finalPath;
        }

        private static string MatchVersionToAssemblyVersion(string[] folders)
        {
            var version = typeof(FolderSearch).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

            foreach (var folder in folders)
            {
                var lastFolder = new DirectoryInfo(folder).Name;
                if (lastFolder == version)
                    return folder;
            }

            return null;
        }
    }
}
