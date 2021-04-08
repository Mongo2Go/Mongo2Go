using System;

namespace Mongo2Go.Helper
{
    /// <summary>
    /// Intention: port numbers won't be assigned twice to avoid connection problems with integration tests
    /// </summary>
    public sealed class PortPool : IPortPool
    {
        private static readonly PortPool Instance = new PortPool();

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
            get { return Instance; }
        }

        /// <summary>
        /// Returns and reserves a new port
        /// </summary>
        public int GetNextOpenPort()
        {
            IPortWatcher portWatcher = PortWatcherFactory.CreatePortWatcher();
            return portWatcher.FindOpenPort();
        }
    }
}
