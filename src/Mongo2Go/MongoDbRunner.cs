using Microsoft.Extensions.Logging;
using Mongo2Go.Helper;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Mongo2Go
{
    /// <summary>
    /// Mongo2Go main entry point
    /// </summary>
    public partial class MongoDbRunner : IDisposable
    {
        private readonly IMongoDbProcess _mongoDbProcess;
        private readonly IFileSystem _fileSystem;
        private readonly string _dataDirectoryWithPort;
        private readonly int _port;
        private readonly IMongoBinaryLocator _mongoBin;

        /// <summary>
        /// State of the current MongoDB instance
        /// </summary>
        public State State { get; private set; }

        /// <summary>
        /// Connections string that should be used to establish a connection the MongoDB instance
        /// </summary>
        public string ConnectionString { get; private set; }

        /// <summary>
        /// Starts Multiple MongoDB instances with each call
        /// On dispose: kills them and deletes their data directory
        /// </summary>
        /// <param name="logger">(Optional) If null, mongod logs are wired to .NET's Console and Debug output (provided you haven't added the --quiet additional argument).
        /// If not null, mongod logs are parsed and wired to the provided logger.</param>
        /// <remarks>Should be used for integration tests</remarks>
        public static MongoDbRunner Start(string dataDirectory = null, string binariesSearchPatternOverride = null, string binariesSearchDirectory = null, bool singleNodeReplSet = false, string additionalMongodArguments = null, ushort singleNodeReplSetWaitTimeout = MongoDbDefaults.SingleNodeReplicaSetWaitTimeout, ILogger logger = null)
        {
            if (dataDirectory == null) {
                dataDirectory = CreateTemporaryDataDirectory();
            }

            // this is required to support multiple instances to run in parallel
            dataDirectory += Guid.NewGuid().ToString().Replace("-", "").Substring(0, 20);

            return new MongoDbRunner(
                PortPool.GetInstance,
                new FileSystem(),
                new MongoDbProcessStarter(),
                new MongoBinaryLocator(binariesSearchPatternOverride, binariesSearchDirectory),
                dataDirectory,
                singleNodeReplSet,
                additionalMongodArguments,
                singleNodeReplSetWaitTimeout,
                logger);
        }

        /// <summary>
        /// !!!
        /// This method is only used for an internal unit test. Use MongoDbRunner.Start() instead.
        /// But if you find it to be useful (eg. to change every aspect on your own) feel free to implement the interfaces on your own!
        /// </summary>
        /// <remarks>see https://github.com/Mongo2Go/Mongo2Go/issues/41 </remarks>
        [Obsolete("Use MongoDbRunner.Start() if possible.")]
        public static MongoDbRunner StartUnitTest(
            IPortPool portPool,
            IFileSystem fileSystem,
            IMongoDbProcessStarter processStarter,
            IMongoBinaryLocator mongoBin,
            string dataDirectory = null,
            string additionalMongodArguments = null)
        {
            return new MongoDbRunner(
                portPool,
                fileSystem,
                processStarter,
                mongoBin,
                dataDirectory,
                additionalMongodArguments: additionalMongodArguments);
        }

        /// <summary>
        /// Only starts one single MongoDB instance (even on multiple calls), does not kill it, does not delete data
        /// </summary>
        /// <remarks>
        /// Should be used for local debugging only
        /// WARNING: one single instance on one single machine is not a suitable setup for productive environments!!!
        /// </remarks>
        public static MongoDbRunner StartForDebugging(string dataDirectory = null, string binariesSearchPatternOverride = null, string binariesSearchDirectory = null, bool singleNodeReplSet = false, int port = MongoDbDefaults.DefaultPort, string additionalMongodArguments = null, ushort singleNodeReplSetWaitTimeout = MongoDbDefaults.SingleNodeReplicaSetWaitTimeout)
        {
            return new MongoDbRunner(
                new ProcessWatcher(),
                new PortWatcher(),
                new FileSystem(),
                new MongoDbProcessStarter(),
                new MongoBinaryLocator(binariesSearchPatternOverride, binariesSearchDirectory), port, dataDirectory, singleNodeReplSet, additionalMongodArguments, singleNodeReplSetWaitTimeout);
        }

        /// <summary>
        /// !!!
        /// This method is only used for an internal unit test. Use MongoDbRunner.StartForDebugging() instead.
        /// But if you find it to be useful (eg. to change every aspect on your own) feel free to implement the interfaces on your own!
        /// </summary>
        /// <remarks>see https://github.com/Mongo2Go/Mongo2Go/issues/41 </remarks>
        [Obsolete("Use MongoDbRunner.StartForDebugging() if possible.")]
        public static MongoDbRunner StartForDebuggingUnitTest(
            IProcessWatcher processWatcher,
            IPortWatcher portWatcher,
            IFileSystem fileSystem,
            IMongoDbProcessStarter processStarter,
            IMongoBinaryLocator mongoBin,
            string dataDirectory = null,
            string additionalMongodArguments = null)
        {
            return new MongoDbRunner(
                processWatcher,
                portWatcher,
                fileSystem,
                processStarter,
                mongoBin,
                MongoDbDefaults.DefaultPort,
                dataDirectory,
                additionalMongodArguments: additionalMongodArguments);
        }

        /// <summary>
        /// Executes Mongoimport on the associated MongoDB Instace
        /// </summary>
        public void Import(string database, string collection, string inputFile, bool drop, string additionalMongodArguments = null)
        {
            MongoImportExport.Import(_mongoBin.Directory, _port, database, collection, inputFile, drop, additionalMongodArguments);
        }

        /// <summary>
        /// Executes Mongoexport on the associated MongoDB Instace
        /// </summary>
        public void Export(string database, string collection, string outputFile, string additionalMongodArguments = null)
        {
            MongoImportExport.Export(_mongoBin.Directory, _port, database, collection, outputFile, additionalMongodArguments);
        }

        /// <summary>
        /// usage: local debugging
        /// </summary>
        private MongoDbRunner(IProcessWatcher processWatcher, IPortWatcher portWatcher, IFileSystem fileSystem, IMongoDbProcessStarter processStarter, IMongoBinaryLocator mongoBin, int port, string dataDirectory = null, bool singleNodeReplSet = false, string additionalMongodArguments = null, ushort singleNodeReplSetWaitTimeout = MongoDbDefaults.SingleNodeReplicaSetWaitTimeout)
        {
            _fileSystem = fileSystem;
            _mongoBin = mongoBin;
            _port = port;

            MakeMongoBinarysExecutable();

            ConnectionString = singleNodeReplSet
                ? "mongodb://127.0.0.1:{0}/?connect=direct&replicaSet=singleNodeReplSet&readPreference=primary".Formatted(_port)
                : "mongodb://127.0.0.1:{0}/".Formatted(_port);

            if (processWatcher.IsProcessRunning(MongoDbDefaults.ProcessName) && !portWatcher.IsPortAvailable(_port))
            {
                State = State.AlreadyRunning;
                return;
            }

            if (!portWatcher.IsPortAvailable(_port))
            {
                throw new MongoDbPortAlreadyTakenException("MongoDB can't be started. The TCP port {0} is already taken.".Formatted(_port));
            }

            if (dataDirectory == null) {
                dataDirectory = CreateTemporaryDataDirectory();
            }

            _fileSystem.CreateFolder(dataDirectory);
            _fileSystem.DeleteFile("{0}{1}{2}".Formatted(dataDirectory, Path.DirectorySeparatorChar.ToString(), MongoDbDefaults.Lockfile));
            _mongoDbProcess = processStarter.Start(_mongoBin.Directory, dataDirectory, _port, true, singleNodeReplSet, additionalMongodArguments, singleNodeReplSetWaitTimeout);

            State = State.Running;
        }

        /// <summary>
        /// usage: integration tests
        /// </summary>
        private MongoDbRunner(IPortPool portPool, IFileSystem fileSystem, IMongoDbProcessStarter processStarter, IMongoBinaryLocator mongoBin, string dataDirectory = null, bool singleNodeReplSet = false, string additionalMongodArguments = null, ushort singleNodeReplSetWaitTimeout = MongoDbDefaults.SingleNodeReplicaSetWaitTimeout, ILogger logger = null)
        {
            _fileSystem = fileSystem;
            _port = portPool.GetNextOpenPort();
            _mongoBin = mongoBin;

            if (dataDirectory == null) {
                dataDirectory = CreateTemporaryDataDirectory();
            }

            MakeMongoBinarysExecutable();

            ConnectionString = singleNodeReplSet
                ? "mongodb://127.0.0.1:{0}/?connect=direct&replicaSet=singleNodeReplSet&readPreference=primary".Formatted(_port)
                : "mongodb://127.0.0.1:{0}/".Formatted(_port);

            _dataDirectoryWithPort = "{0}_{1}".Formatted(dataDirectory, _port);
            _fileSystem.CreateFolder(_dataDirectoryWithPort);
            _fileSystem.DeleteFile("{0}{1}{2}".Formatted(_dataDirectoryWithPort, Path.DirectorySeparatorChar.ToString(), MongoDbDefaults.Lockfile));

            _mongoDbProcess = processStarter.Start(_mongoBin.Directory, _dataDirectoryWithPort, _port, singleNodeReplSet, additionalMongodArguments, singleNodeReplSetWaitTimeout, logger);

            State = State.Running;
        }

        private void MakeMongoBinarysExecutable()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
                RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _fileSystem.MakeFileExecutable(Path.Combine(_mongoBin.Directory, MongoDbDefaults.MongodExecutable));
                _fileSystem.MakeFileExecutable(Path.Combine(_mongoBin.Directory, MongoDbDefaults.MongoExportExecutable));
                _fileSystem.MakeFileExecutable(Path.Combine(_mongoBin.Directory, MongoDbDefaults.MongoImportExecutable));
            }
        }

        private static string CreateTemporaryDataDirectory() {
            var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(path);
            return path;
        }
    }
}