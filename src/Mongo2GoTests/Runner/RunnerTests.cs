using System;
using System.IO;
using FluentAssertions;
using Machine.Specifications;
using Mongo2Go;
using Mongo2Go.Helper;
using Moq;
using It = Machine.Specifications.It;

#pragma warning disable CS0618 // Type or member is obsolete

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local

namespace Mongo2GoTests.Runner
{
    [Subject("Runner")]
    public class when_instanciating_the_runner_for_integration_test
    {
        static MongoDbRunner runner;
        static Mock<IPortPool> portPoolMock;
        static Mock<IFileSystem> fileSystemMock;
        static Mock<IMongoDbProcessStarter> processStarterMock;
        static Mock<IMongoBinaryLocator> binaryLocatorMock;

        static string exptectedDataDirectory;
        static string exptectedLogfile;
        static readonly string exptectedConnectString = "mongodb://localhost:{0}/".Formatted(MongoDbDefaults.TestStartPort + 1);

        Establish context = () =>
        {
            portPoolMock = new Mock<IPortPool>();
            portPoolMock.Setup(m => m.GetNextOpenPort()).Returns(MongoDbDefaults.TestStartPort + 1);

            fileSystemMock = new Mock<IFileSystem>();
            fileSystemMock.Setup(m => m.CreateFolder(Moq.It.IsAny<string>())).Callback<string>(s => 
            {
                exptectedDataDirectory = s;
                exptectedLogfile = Path.Combine(exptectedDataDirectory, MongoDbDefaults.Lockfile);
            });
            
            var processMock = new Mock<IMongoDbProcess>();

            processStarterMock = new Mock<IMongoDbProcessStarter>();
            processStarterMock.Setup(m => m.Start(Moq.It.IsAny<string>(), Moq.It.IsAny<string>(), Moq.It.IsAny<int>())).Returns(processMock.Object);

            binaryLocatorMock = new Mock<IMongoBinaryLocator> ();
            binaryLocatorMock.Setup(m => m.Directory).Returns(string.Empty);
        };

        Because of = () => runner = MongoDbRunner.StartUnitTest(portPoolMock.Object, fileSystemMock.Object, processStarterMock.Object, binaryLocatorMock.Object);

        It should_create_the_data_directory             = () => fileSystemMock.Verify(x => x.CreateFolder(Moq.It.Is<string>(s => s.StartsWith(Path.GetTempPath()))), Times.Exactly(1));
        It should_delete_old_lock_file                  = () => fileSystemMock.Verify(x => x.DeleteFile(exptectedLogfile), Times.Exactly(1));

        It should_start_the_process                     = () => processStarterMock.Verify(x => x.Start(Moq.It.IsAny<string>(), Moq.It.IsAny<string>(), Moq.It.IsAny<int>()), Times.Exactly(1));

        It should_have_expected_connection_string       = () => runner.ConnectionString.Should().Be(exptectedConnectString);
		It should_return_an_instance_with_state_running = () => runner.State.Should().Be(State.Running);
    }

    [Subject("Runner")]
    public class when_instanciating_the_runner_for_local_debugging
    {
        static MongoDbRunner runner;
        static Mock<IPortWatcher> portWatcherMock;
        static Mock<IProcessWatcher> processWatcherMock;
        static Mock<IFileSystem> fileSystemMock;
        static Mock<IMongoDbProcessStarter> processStarterMock;
        static Mock<IMongoBinaryLocator> binaryLocatorMock;

        static string exptectedDataDirectory;
        static string exptectedLogfile;

        Establish context = () =>
        {
            processWatcherMock = new Mock<IProcessWatcher>();
            processWatcherMock.Setup(m => m.IsProcessRunning(Moq.It.IsAny<string>())).Returns(false);

            portWatcherMock = new Mock<IPortWatcher>();
            portWatcherMock.Setup(m => m.IsPortAvailable(Moq.It.IsAny<int>())).Returns(true);

            fileSystemMock = new Mock<IFileSystem>();
            fileSystemMock.Setup(m => m.CreateFolder(Moq.It.IsAny<string>())).Callback<string>(s => 
            {
                exptectedDataDirectory = s;
                exptectedLogfile = Path.Combine(exptectedDataDirectory, MongoDbDefaults.Lockfile);
            });

            var processMock = new Mock<IMongoDbProcess>();
            processStarterMock = new Mock<IMongoDbProcessStarter>();
            processStarterMock.Setup(m => m.Start(Moq.It.IsAny<string>(), exptectedDataDirectory, MongoDbDefaults.DefaultPort, true)).Returns(processMock.Object);

            binaryLocatorMock = new Mock<IMongoBinaryLocator> ();
            binaryLocatorMock.Setup(m => m.Directory).Returns(string.Empty);
        };

        Because of = () => runner = MongoDbRunner.StartForDebuggingUnitTest(processWatcherMock.Object, portWatcherMock.Object, fileSystemMock.Object, processStarterMock.Object, binaryLocatorMock.Object);

        It should_check_for_already_running_process = () => processWatcherMock.Verify(x => x.IsProcessRunning(MongoDbDefaults.ProcessName), Times.Exactly(1));
        It should_check_the_default_port = () => portWatcherMock.Verify(x => x.IsPortAvailable(MongoDbDefaults.DefaultPort), Times.Exactly(1));
        It should_create_the_data_directory = () => fileSystemMock.Verify(x => x.CreateFolder(Moq.It.Is<string>(s => s.StartsWith(Path.GetTempPath()))), Times.Exactly(1));
        It should_delete_old_lock_file = () => fileSystemMock.Verify(x => x.DeleteFile(exptectedLogfile), Times.Exactly(1));
		It should_return_an_instance_with_state_running = () => runner.State.Should().Be(State.Running);
        It should_start_the_process_without_kill = () => processStarterMock.Verify(x => x.Start(Moq.It.IsAny<string>(), exptectedDataDirectory, MongoDbDefaults.DefaultPort, true), Times.Exactly(1));
    }
}
// ReSharper restore UnusedMember.Local
// ReSharper restore InconsistentNaming