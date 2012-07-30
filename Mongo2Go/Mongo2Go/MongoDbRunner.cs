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
        private readonly IFileSystem _fileSystem;

        private const string BinariesSearchPattern = @"packages\Mongo2Go*\tools\mongodb-win32-i386*\bin";
        private const string BinariesSearchPatternDebug = @"tools\mongodb-win32-i386*\bin";

        public State State { get; private set; }

        private MongoDbRunner(IProcessWatcher processWatcher, IPortWatcher portWatcher, IFileSystem fileSystem)
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

            
            

            State = State.Running;
        }

        public static MongoDbRunner Start()
        {
            return new MongoDbRunner(new ProcessWatcher(), new PortWatcher(), new FileSystem());
        }

        internal static MongoDbRunner StartForUnitTest(IProcessWatcher processWatcher, IPortWatcher portWatcher, IFileSystem fileSystem)
        {
            return new MongoDbRunner(processWatcher, portWatcher, fileSystem);
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
            if (State == State.Running && disposing)
            {
                // TODO: kill process
            }
        }

        #endregion
    }
}
