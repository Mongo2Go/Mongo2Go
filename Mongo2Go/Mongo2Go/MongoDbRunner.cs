using System;
using Mongo2Go.Helper;

namespace Mongo2Go
{
    public class MongoDbRunner : IDisposable
    {
        private readonly IProcessWatcher _processWatcher;        
        private readonly IPortWatcher _portWatcher;
        private readonly IFileSystem _fileSystem;
        private readonly IMongoDbProcess _process;
        private readonly string _dataDirectoryWithPort;

        private const string BinariesSearchPattern = @"packages\Mongo2Go*\tools\mongodb-win32-i386*\bin";
        private const string BinariesSearchPatternSolution = @"tools\mongodb-win32-i386*\bin";

        public bool Disposed { get; private set; }
        public State State { get; private set; }
        public int Port { get; private set; }
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Starts Multiple MongoDB instances with each call
        /// On dispose: kills them and deletes their data directory
        /// </summary>
        /// <remarks>Should be used for integration tests</remarks>
        public static MongoDbRunner Start()
        {
            return new MongoDbRunner(new PortWatcher(), new FileSystem(), new MongoDbProcess(null));
        }

        internal static MongoDbRunner StartUnitTest(IPortWatcher portWatcher, IFileSystem fileSystem, IMongoDbProcess processStarter)
        {
            return new MongoDbRunner(portWatcher, fileSystem, processStarter);
        }

        /// <summary>
        /// Only starts one single MongoDB instance (even on multiple calls), does not kill it, does not delete data
        /// </summary>
        /// <remarks>
        /// Should be used for local debugging only
        /// WARNING: one single instance on one single machine is not a suitable setup for productive environments!!!
        /// </remarks>
        public static MongoDbRunner StartForDebugging()
        {
            return new MongoDbRunner(new ProcessWatcher(), new PortWatcher(), new FileSystem(), new MongoDbProcess(null));
        }

        internal static MongoDbRunner StartForDebuggingUnitTest(IProcessWatcher processWatcher, IPortWatcher portWatcher, IFileSystem fileSystem, IMongoDbProcess processStarter)
        {
            return new MongoDbRunner(processWatcher, portWatcher, fileSystem, processStarter);
        }

        /// <summary>
        /// Excutes Mongoimport on the associated MongoDB Instace
        /// </summary>
        public void Import(string database, string collection, string inputFile, bool drop)
        {
            MongoImportExport.Import(BinariesDirectory, Port, database, collection, inputFile, drop);
        }

        /// <summary>
        /// Excutes Mongoexport on the associated MongoDB Instace
        /// </summary>
        public void Export(string database, string collection, string outputFile)
        {
            MongoImportExport.Export(BinariesDirectory, Port, database, collection, outputFile);
        }
        
        /// <summary>
        /// usage: local debugging
        /// </summary>
        private MongoDbRunner(IProcessWatcher processWatcher, IPortWatcher portWatcher, IFileSystem fileSystem, IMongoDbProcess processStarter)
        {
            _processWatcher = processWatcher;            
            _portWatcher = portWatcher;
            _fileSystem = fileSystem;

            Port = MongoDbDefaults.DefaultPort;
            ConnectionString = "mongodb://localhost:{0}/".Formatted(Port);

            if (_processWatcher.IsProcessRunning(MongoDbDefaults.ProcessName))
            {
                State = State.AlreadyRunning;
                return;
            }

            if (!_portWatcher.IsPortAvailable(Port))
            {
                throw new MongoDbPortAlreadyTakenException("MongoDB can't be started. The TCP port {0} is already taken.".Formatted(Port));
            }

            _fileSystem.CreateFolder(MongoDbDefaults.DataDirectory);
            _fileSystem.DeleteFile(@"{0}\{1}".Formatted(MongoDbDefaults.DataDirectory, MongoDbDefaults.Lockfile));
            _process = processStarter.Start(BinariesDirectory, MongoDbDefaults.DataDirectory, Port, true);

            State = State.Running;
        }

        /// <summary>
        /// usage: integration tests
        /// </summary>
        private MongoDbRunner(IPortWatcher portWatcher, IFileSystem fileSystem, IMongoDbProcess processStarter)
        {
            _portWatcher = portWatcher;
            _fileSystem = fileSystem;

            Port = _portWatcher.FindOpenPort(MongoDbDefaults.TestStartPort);
            ConnectionString = "mongodb://localhost:{0}/".Formatted(Port);

            _dataDirectoryWithPort = "{0}_{1}".Formatted(MongoDbDefaults.DataDirectory, Port);
            _fileSystem.CreateFolder(_dataDirectoryWithPort);
            _fileSystem.DeleteFile(@"{0}\{1}".Formatted(_dataDirectoryWithPort, MongoDbDefaults.Lockfile));
            _process = processStarter.Start(BinariesDirectory, _dataDirectoryWithPort, Port);

            State = State.Running;
        }

        private static string BinariesDirectory
        {
            get
            {
                // 1st: path when installed via nuget
                // 2nd: path when started from solution
                string binariesFolder = FolderSearch.CurrentExecutingDirectory().FindFolderUpwards(BinariesSearchPattern) ??
                                        FolderSearch.CurrentExecutingDirectory().FindFolderUpwards(BinariesSearchPatternSolution);

                if (binariesFolder == null)
                {
                    throw new MonogDbBinariesNotFoundException();
                }
                return binariesFolder;
            }
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

            // will be null if we are working in debugging mode (single instance)
            if (_dataDirectoryWithPort != null) {
                // finally clean up the data directory we created previously
                _fileSystem.DeleteFolder(_dataDirectoryWithPort);
            }

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