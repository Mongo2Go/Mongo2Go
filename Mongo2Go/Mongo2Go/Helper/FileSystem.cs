using System.IO;

namespace Mongo2Go.Helper
{
    public class FileSystem : IFileSystem
    {
        public void CreateFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public void DeleteFolder(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        public void DeleteFile(string fullFileName)
        {
            if (File.Exists(fullFileName))
            {
                File.Delete(fullFileName);
            }
        }
    }
}
