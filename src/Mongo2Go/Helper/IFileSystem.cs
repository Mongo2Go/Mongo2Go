namespace Mongo2Go.Helper
{
    public interface IFileSystem
    {
        void CreateFolder(string path);
        void DeleteFolder(string path);
        void DeleteFile(string fullFileName);
    }
}