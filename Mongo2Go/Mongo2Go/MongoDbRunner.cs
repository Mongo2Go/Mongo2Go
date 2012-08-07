using System;
using System.Globalization;
using Mongo2Go.Helper;

namespace Mongo2Go
{
    public class MongoDbRunner : IDisposable
    {
        private readonly IProcessWatcher _processWatcher;
        private readonly IPortWatcher _portWatcher;
        private readonly IFileSystem _fileSystem;
        private readonly IMongoDbProcess _process;

        private const string BinariesSearchPattern = @"packages\Mongo2Go*\tools\mongodb-win32-i386*\bin";
        private const string BinariesSearchPatternDebug = @"tools\mongodb-win32-i386*\bin";

        public bool Disposed { get; private set; }
        public State State { get; private set; }

        private MongoDbRunner(IProcessWatcher processWatcher, IPortWatcher portWatcher, IFileSystem fileSystem, IMongoDbProcess processStarter)
        {
            _processWatcher = processWatcher;
            _portWatcher = portWatcher;
            _fileSystem = fileSystem;

            if (_processWatcher.IsProcessRunning(MongoDbDefaults.ProcessName))
            {
                State = State.AlreadyRunning;
                return;
            }

            if (!_portWatcher.IsPortAvailable(MongoDbDefaults.Port))
            {
                throw MongoDbPortAlreadyTakenException();
            }

            // 1st: path when installed via nuget
            // 2nd: path when started from solution
            string binariesFolder = FolderSearch.CurrentExecutingDirectory().FindFolderUpwards(BinariesSearchPattern) ??
                                    FolderSearch.CurrentExecutingDirectory().FindFolderUpwards(BinariesSearchPatternDebug);

            if (binariesFolder == null)
            {
                throw new MonogDbBinariesNotFoundException();
            }

            _fileSystem.CreateFolder(MongoDbDefaults.DataFolder);
            _fileSystem.DeleteFile(MongoDbDefaults.Lockfile);

            _process = processStarter.Start(binariesFolder);

            State = State.Running;
        }

        public static MongoDbRunner Start()
        {
            return new MongoDbRunner(new ProcessWatcher(), new PortWatcher(), new FileSystem(), new MongoDbProcess(null));
        }

        internal static MongoDbRunner StartForUnitTest(IProcessWatcher processWatcher, IPortWatcher portWatcher, IFileSystem fileSystem, IMongoDbProcess processStarter)
        {
            return new MongoDbRunner(processWatcher, portWatcher, fileSystem, processStarter);
        }

        private static MongoDbPortAlreadyTakenException MongoDbPortAlreadyTakenException()
        {
            string message = string.Format(CultureInfo.InvariantCulture, "MongoDB can't be started. The TCP port {0} is already taken.", MongoDbDefaults.Port);
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
            _fileSystem.DeleteFolder(MongoDbDefaults.DataFolder);

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