using Machine.Specifications;
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
        private static MongoDbRunner runner;
        private static Mock<IPortWatcher> portMock;
        private static Mock<IProcessWatcher> watcherMock;
        private static Mock<IFileSystem> fileMock;
        private static Mock<IMongoDbProcess> processMock;

        Establish context = () =>
        {
            watcherMock = new Mock<IProcessWatcher>();
            watcherMock.Setup(m => m.IsProcessRunning(Moq.It.IsAny<string>())).Returns(false);
            
            portMock = new Mock<IPortWatcher>();
            portMock.Setup(m => m.IsPortAvailable(Moq.It.IsAny<int>())).Returns(true);

            fileMock = new Mock<IFileSystem>();
            
            var innerProcessMock = new Mock<IMongoDbProcess>();
            processMock = new Mock<IMongoDbProcess>();
            processMock.Setup(m => m.Start(Moq.It.IsAny<string>())).Returns(innerProcessMock.Object);
        };

        Because of = () => runner = MongoDbRunner.StartForUnitTest(watcherMock.Object, portMock.Object, fileMock.Object, processMock.Object);

        It should_check_for_already_running_process      = () => watcherMock.Verify(x => x.IsProcessRunning(MongoDbDefaults.ProcessName), Times.Exactly(1));
        It should_check_the_default_port                 = () => portMock.Verify(x => x.IsPortAvailable(MongoDbDefaults.Port), Times.Exactly(1));
        It should_create_the_data_directory              = () => fileMock.Verify(x => x.CreateFolder(MongoDbDefaults.DataFolder), Times.Exactly(1));
        It should_delete_old_lock_file                   = () => fileMock.Verify(x => x.DeleteFile(MongoDbDefaults.Lockfile), Times.Exactly(1));
        It should_return_an_instance_with_state_running  = () => runner.State.ShouldEqual(State.Running);
        It should_start_the_process                      = () => processMock.Verify(x => x.Start(Moq.It.IsAny<string>()), Times.Exactly(1));
    }
}
// ReSharper restore InconsistentNaming