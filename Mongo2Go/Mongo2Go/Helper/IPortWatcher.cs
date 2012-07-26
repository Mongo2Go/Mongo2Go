namespace Mongo2Go.Helper
{
    public interface IPortWatcher
    {
        bool IsPortAvailable(int portNumber);
    }
}