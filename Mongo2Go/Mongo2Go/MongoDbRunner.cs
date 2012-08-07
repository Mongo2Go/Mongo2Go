using System;
using System.Globalization;
using Mongo2Go.Helper;

namespace Mongo2Go
{
    public class MongoDbRunner : IDisposable
    {
        private readonly IPortWatcher _portWatcher;
        private readonly IFileSystem _fileSystem;
        private readonly IMongoDbProcess _process;

        private const string BinariesSearchPattern = @"packages\Mongo2Go*\tools\mongodb-win32-i386*\bin";
        private const string BinariesSearchPatternDebug = @"tools\mongodb-win32-i386*\bin";

        public bool Disposed { get; private set; }
        public State State { get; private set; }
        public string ConnectionString { get; private set; }

        private MongoDbRunner(IPortWatcher portWatcher, IFileSystem fileSystem, IMongoDbProcess processStarter)
        {
            _portWatcher = portWatcher;
            _fileSystem = fileSystem;

            // 1st: path when installed via nuget
            // 2nd: path when started from solution
            string binariesFolder = FolderSearch.CurrentExecutingDirectory().FindFolderUpwards(BinariesSearchPattern) ??
                                    FolderSearch.CurrentExecutingDirectory().FindFolderUpwards(BinariesSearchPatternDebug);

            if (binariesFolder == null)
            {
                throw new MonogDbBinariesNotFoundException();
            }

            int port = FindOpenPort();
            string dataDirectory = MongoDbDefaults.DataDirectory + "_" + port;

            _fileSystem.CreateFolder(dataDirectory);
            _fileSystem.DeleteFile(dataDirectory + "\\" + MongoDbDefaults.Lockfile);
            _process = processStarter.Start(binariesFolder, dataDirectory, port);

            ConnectionString = string.Format(CultureInfo.InvariantCulture, "mongodb://localhost:{0}/", port);
            State = State.Running;
}

        private int FindOpenPort()
        {
            int port = MongoDbDefaults.Port;
            do
            {
                if (_portWatcher.IsPortAvailable(port))
                {
                    break;
                }

                if (port == MongoDbDefaults.Port + 100) { 
                    throw new NoFreePortFoundException();
                }

                ++port;

            } while (true);

            return port;
        }

        public static MongoDbRunner Start()
        {
            return new MongoDbRunner(new PortWatcher(), new FileSystem(), new MongoDbProcess(null));
        }

        internal static MongoDbRunner StartForUnitTest(IPortWatcher portWatcher, IFileSystem fileSystem, IMongoDbProcess processStarter)
        {
            return new MongoDbRunner(portWatcher, fileSystem, processStarter);
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Disposed) { return; }
            if (State != State.Running) { return; }

            if (disposing)
            {
                // we have no "managed resources" - but we leave this switch to avoid an FxCop CA1801 warnig
            }

            if (_process != null)
            {
                _process.Dispose();
            }

            // finally clean up the data directory we created previously
            _fileSystem.DeleteFolder(MongoDbDefaults.DataDirectory);

            Disposed = true;
            State = State.Stopped;
        }

        ~MongoDbRunner()
        {
            Dispose(false);
        }

        #endregion
    }
}