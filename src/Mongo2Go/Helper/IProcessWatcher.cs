namespace Mongo2Go.Helper
{
    public interface IProcessWatcher
    {
        bool IsProcessRunning(string processName);
    }
}