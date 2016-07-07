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

        public void MakeFileExecutable (string path) 
        {
            //when on linux or osx we must set the executeble flag on mongo binarys
            File.SetAttributes (path, (FileAttributes)((int)File.GetAttributes (path) | 0x80000000));
        }
    }
}
