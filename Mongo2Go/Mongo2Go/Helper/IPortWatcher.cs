namespace Mongo2Go.Helper
{
    public interface IPortWatcher
    {
        int FindOpenPort(int startPort);
        bool IsPortAvailable(int portNumber);
    }
}