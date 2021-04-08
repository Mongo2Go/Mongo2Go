namespace Mongo2Go.Helper
{
    public interface IPortWatcher
    {
        int FindOpenPort();
        bool IsPortAvailable(int portNumber);
    }
}
