using System;

namespace Mongo2Go.Helper
{
    /// <summary>
    /// Intention: port numbers won't be assigned twice to avoid connection problems with integration tests
    /// </summary>
    public sealed class PortPool : IPortPool
    {
        private readonly Object _lock = new Object();
        private static readonly PortPool Instance = new PortPool();

        private int _startPort = MongoDbDefaults.TestStartPort;

        // Explicit static constructor to tell C# compiler
        // not to mark type as beforefieldinit
        static PortPool()
        {
        }

        // Singleton
        private PortPool()
        {
        }

        public static PortPool GetInstance
        {
            get
            {
                return Instance;
            }
        }

        /// <summary>
        /// Returns and reserves a new port
        /// </summary>
        public int GetNextOpenPort()
        {
            lock (_lock)
            {
                PortWatcher portWatcher = new PortWatcher();
                int newAvailablePort = portWatcher.FindOpenPort(_startPort);

                _startPort = newAvailablePort + 1;
                return newAvailablePort;
            }
        }
    }
}
