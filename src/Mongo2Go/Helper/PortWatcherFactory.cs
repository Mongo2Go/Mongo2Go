using System.Runtime.InteropServices;

namespace Mongo2Go.Helper
{
    public class PortWatcherFactory 
    {
        public static IPortWatcher CreatePortWatcher()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                ? (IPortWatcher) new UnixPortWatcher()
                : new PortWatcher();
        }
    }   
}