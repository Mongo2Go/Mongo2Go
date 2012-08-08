﻿using Machine.Specifications;
using Mongo2Go;
using Mongo2Go.Helper;
using Moq;
using It = Machine.Specifications.It;

// ReSharper disable InconsistentNaming
namespace Mongo2GoTests.Runner
{
    [Subject("Runner")]
    public class when_instanciating_the_runner
    {
        static MongoDbRunner runner;
        static Mock<IPortWatcher> portWatcherMock;
        static Mock<IFileSystem> fileMock;
        static Mock<IMongoDbProcess> processMock;

        static readonly string exptectedDataDirectory = "{0}_{1}".Formatted(MongoDbDefaults.DataDirectory, MongoDbDefaults.TestStartPort + 1);
        static readonly string exptectedLogfile = @"{0}_{1}\{2}".Formatted(MongoDbDefaults.DataDirectory, MongoDbDefaults.TestStartPort + 1, MongoDbDefaults.Lockfile);
        static readonly string exptectedConnectString = "mongodb://localhost:{0}/".Formatted(MongoDbDefaults.TestStartPort + 1);

        Establish context = () =>
        {
            portWatcherMock = new Mock<IPortWatcher>();
            portWatcherMock.Setup(m => m.FindOpenPort(MongoDbDefaults.TestStartPort)).Returns(MongoDbDefaults.TestStartPort + 1);

            fileMock = new Mock<IFileSystem>();
            
            var innerProcessMock = new Mock<IMongoDbProcess>();
            processMock = new Mock<IMongoDbProcess>();
            processMock.Setup(m => m.Start(Moq.It.IsAny<string>(), Moq.It.IsAny<string>(), Moq.It.IsAny<int>())).Returns(innerProcessMock.Object);
        };

        Because of = () => runner = MongoDbRunner.StartForUnitTest(portWatcherMock.Object, fileMock.Object, processMock.Object);

        It should_create_the_data_directory             = () => fileMock.Verify(x => x.CreateFolder(exptectedDataDirectory), Times.Exactly(1));
        It should_delete_old_lock_file                  = () => fileMock.Verify(x => x.DeleteFile(exptectedLogfile), Times.Exactly(1));

        It should_start_the_process                     = () => processMock.Verify(x => x.Start(Moq.It.IsAny<string>(), Moq.It.IsAny<string>(), Moq.It.IsAny<int>()), Times.Exactly(1));

        It should_have_expected_connection_string       = () => runner.ConnectionString.ShouldEqual(exptectedConnectString);
        It should_return_an_instance_with_state_running = () => runner.State.ShouldEqual(State.Running);
    }
}
// ReSharper restore InconsistentNaming