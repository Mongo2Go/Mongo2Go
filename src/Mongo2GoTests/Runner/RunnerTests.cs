using Machine.Specifications;
using Mongo2Go;
using Mongo2Go.Helper;
using Moq;
using It = Machine.Specifications.It;

// ReSharper disable InconsistentNaming
namespace Mongo2GoTests.Runner
{
    [Subject("Runner")]
    public class when_instanciating_the_runner_for_integration_test
    {
        static MongoDbRunner runner;
        static Mock<IPortPool> portPoolMock;
        static Mock<IFileSystem> fileSystemMock;
        static Mock<IMongoDbProcessStarter> processStarterMock;

        static readonly string exptectedDataDirectory = "{0}_{1}".Formatted(MongoDbDefaults.DataDirectory, MongoDbDefaults.TestStartPort + 1);
        static readonly string exptectedLogfile = @"{0}_{1}\{2}".Formatted(MongoDbDefaults.DataDirectory, MongoDbDefaults.TestStartPort + 1, MongoDbDefaults.Lockfile);
        static readonly string exptectedConnectString = "mongodb://localhost:{0}/".Formatted(MongoDbDefaults.TestStartPort + 1);

        Establish context = () =>
        {
            portPoolMock = new Mock<IPortPool>();
            portPoolMock.Setup(m => m.GetNextOpenPort()).Returns(MongoDbDefaults.TestStartPort + 1);

            fileSystemMock = new Mock<IFileSystem>();
            
            var processMock = new Mock<IMongoDbProcess>();

            processStarterMock = new Mock<IMongoDbProcessStarter>();
            processStarterMock.Setup(m => m.Start(Moq.It.IsAny<string>(), Moq.It.IsAny<string>(), Moq.It.IsAny<int>())).Returns(processMock.Object);
        };

        Because of = () => runner = MongoDbRunner.StartUnitTest(portPoolMock.Object, fileSystemMock.Object, processStarterMock.Object);

        It should_create_the_data_directory             = () => fileSystemMock.Verify(x => x.CreateFolder(exptectedDataDirectory), Times.Exactly(1));
        It should_delete_old_lock_file                  = () => fileSystemMock.Verify(x => x.DeleteFile(exptectedLogfile), Times.Exactly(1));

        It should_start_the_process                     = () => processStarterMock.Verify(x => x.Start(Moq.It.IsAny<string>(), Moq.It.IsAny<string>(), Moq.It.IsAny<int>()), Times.Exactly(1));

        It should_have_expected_connection_string       = () => runner.ConnectionString.ShouldEqual(exptectedConnectString);
        It should_return_an_instance_with_state_running = () => runner.State.ShouldEqual(State.Running);
    }

    [Subject("Runner")]
    public class when_instanciating_the_runner_for_local_debugging
    {
        static MongoDbRunner runner;
        static Mock<IPortWatcher> portWatcherMock;
        static Mock<IProcessWatcher> processWatcherMock;
        static Mock<IFileSystem> fileSystemMock;
        static Mock<IMongoDbProcessStarter> processStarterMock;

        static readonly string exptectedLogfile = @"{0}\{1}".Formatted(MongoDbDefaults.DataDirectory, MongoDbDefaults.Lockfile);

        Establish context = () =>
        {
            processWatcherMock = new Mock<IProcessWatcher>();
            processWatcherMock.Setup(m => m.IsProcessRunning(Moq.It.IsAny<string>())).Returns(false);

            portWatcherMock = new Mock<IPortWatcher>();
            portWatcherMock.Setup(m => m.IsPortAvailable(Moq.It.IsAny<int>())).Returns(true);

            fileSystemMock = new Mock<IFileSystem>();

            var processMock = new Mock<IMongoDbProcess>();
            processStarterMock = new Mock<IMongoDbProcessStarter>();
            processStarterMock.Setup(m => m.Start(Moq.It.IsAny<string>(), MongoDbDefaults.DataDirectory, MongoDbDefaults.DefaultPort, true)).Returns(processMock.Object);
        };

        Because of = () => runner = MongoDbRunner.StartForDebuggingUnitTest(processWatcherMock.Object, portWatcherMock.Object, fileSystemMock.Object, processStarterMock.Object);

        It should_check_for_already_running_process = () => processWatcherMock.Verify(x => x.IsProcessRunning(MongoDbDefaults.ProcessName), Times.Exactly(1));
        It should_check_the_default_port = () => portWatcherMock.Verify(x => x.IsPortAvailable(MongoDbDefaults.DefaultPort), Times.Exactly(1));
        It should_create_the_data_directory = () => fileSystemMock.Verify(x => x.CreateFolder(MongoDbDefaults.DataDirectory), Times.Exactly(1));
        It should_delete_old_lock_file = () => fileSystemMock.Verify(x => x.DeleteFile(exptectedLogfile), Times.Exactly(1));
        It should_return_an_instance_with_state_running = () => runner.State.ShouldEqual(State.Running);
        It should_start_the_process_without_kill = () => processStarterMock.Verify(x => x.Start(Moq.It.IsAny<string>(), MongoDbDefaults.DataDirectory, MongoDbDefaults.DefaultPort, true), Times.Exactly(1));
    }
}
// ReSharper restore InconsistentNaming