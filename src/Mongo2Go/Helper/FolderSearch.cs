using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

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

        /// <summary>
        /// Enumerates every folder matching <paramref name="searchPattern"/>, starting at <paramref name="startPath"/>
        /// and walking upwards, nearest match first.
        /// </summary>
        /// <remarks>
        /// <see cref="FindFolderUpwards"/> stops at the first match, which is enough when any match will do. Callers
        /// that validate what they find - see <see cref="MongoBinaryManifest"/> - need to be able to reject a candidate
        /// and carry on, otherwise an unusable directory encountered early would mask a valid one further up.
        /// Unlike <see cref="FindFolder"/>, this yields <em>all</em> siblings matching a pattern part rather than
        /// collapsing them to one, so a rejected sibling cannot hide a valid one at the same level.
        /// </remarks>
        public static IEnumerable<string> FindFoldersUpwards(this string startPath, string searchPattern)
        {
            if (searchPattern == null)
            {
                yield break;
            }

            for (string current = startPath; !string.IsNullOrEmpty(current); current = current.RemoveLastPart())
            {
                foreach (string match in FindFolders(current, searchPattern))
                {
                    yield return match;
                }
            }
        }

        private static IEnumerable<string> FindFolders(string startPath, string searchPattern)
        {
            IEnumerable<string> currentPaths = new[] { startPath };

            foreach (var part in searchPattern.Split(_separators, StringSplitOptions.None))
            {
                currentPaths = currentPaths
                    .Where(Directory.Exists)
                    .SelectMany(path => Directory.GetDirectories(path, part))
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList();
            }

            return currentPaths;
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
