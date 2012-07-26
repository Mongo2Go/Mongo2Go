using System;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Mongo2Go.Helper
{
    public static class FileSystem
    {
        private const int MaxLevelOfRecursion = 6;

        public static string GetCurrentExecutingDirectory()
        {
            string filePath = new Uri(Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            return Path.GetDirectoryName(filePath);
        }

        public static string FindFolderRecursively(this string currentPath, string folderName, int currentLevel = 0)
        {
            if (currentLevel >= MaxLevelOfRecursion)
            {
                //string message = string.Format(CultureInfo.InvariantCulture, "The folder {0} was not found. Last try: {1}", folderName, Path.GetFullPath(currentPath));
                //throw new FileNotFoundException(message);
                return null;
            }

            if (Directory.Exists(currentPath + "\\" + folderName))
            {
                return currentPath + "\\" + folderName;
            }

            return FindFolderRecursively(currentPath + "\\..", folderName, currentLevel + 1);
        }
    }
}
