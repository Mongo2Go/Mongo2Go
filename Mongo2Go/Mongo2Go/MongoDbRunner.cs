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
        private readonly string _dataDirectoryWithPort;

        private const string BinariesSearchPattern = @"packages\Mongo2Go*\tools\mongodb-win32-i386*\bin";
        private const string BinariesSearchPatternDebug = @"tools\mongodb-win32-i386*\bin";

        public bool Disposed { get; private set; }
        public State State { get; private set; }
        public int Port { get; private set; }
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

            Port = _portWatcher.FindOpenPort(MongoDbDefaults.Port);
            ConnectionString = "mongodb://localhost:{0}/".Formatted(Port);

            _dataDirectoryWithPort = "{0}_{1}".Formatted(MongoDbDefaults.DataDirectory, Port);
            _fileSystem.CreateFolder(_dataDirectoryWithPort);
            _fileSystem.DeleteFile(@"{0}\{1}".Formatted(_dataDirectoryWithPort, MongoDbDefaults.Lockfile));
            _process = processStarter.Start(binariesFolder, _dataDirectoryWithPort, Port);

            State = State.Running;
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
            _fileSystem.DeleteFolder(_dataDirectoryWithPort);

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