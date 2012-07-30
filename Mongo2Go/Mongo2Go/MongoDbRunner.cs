using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Mongo2Go.Helper;

namespace Mongo2Go
{
    public class MongoDbRunner : IDisposable
    {
        private readonly IProcessWatcher _processWatcher;
        private readonly IPortWatcher _portWatcher;
        
        public const string MongoDbProcessName = "mongod";
        public const int MongoDbDefaultPort = 27017;

        public bool Running { get; private set; }

        private MongoDbRunner(IProcessWatcher processWatcher, IPortWatcher portWatcher)
        {
            _processWatcher = processWatcher;
            _portWatcher = portWatcher;

            if (_processWatcher.IsProcessRunning(MongoDbProcessName))
            {
                Running = true;
                return;
            }

            if (!_portWatcher.IsPortAvailable(MongoDbDefaultPort))
            {
                throw MongoDbPortAlreadyTakenException();
            }

            //string myFolder = FileSystem.CurrentExecutingDirectory().FindFolderRecursively("tools");
            //if (myFolder != null)
            //{
                
            //}

            Running = true;
        }

        public static MongoDbRunner Start()
        {
            return new MongoDbRunner(new ProcessWatcher(),new PortWatcher());
        }

        internal static MongoDbRunner StartForUnitTest(IProcessWatcher processWatcher, IPortWatcher portWatcher)
        {
            return new MongoDbRunner(processWatcher, portWatcher);
        }

        private static MongoDbPortAlreadyTakenException MongoDbPortAlreadyTakenException()
        {
            string message = string.Format(CultureInfo.InvariantCulture, "MongoDB can't be started. The TCP port {0} is already taken.", MongoDbDefaultPort);
            return new MongoDbPortAlreadyTakenException(message);
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Running && disposing)
            {
                
            }
        }

        #endregion
    }
}
