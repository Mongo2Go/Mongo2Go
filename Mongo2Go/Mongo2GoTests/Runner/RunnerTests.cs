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
        private static Mock<IProcessWatcher> processMock;

        Establish context = () =>
        {
            processMock = new Mock<IProcessWatcher>();
            processMock.Setup(m => m.IsProcessRunning(Moq.It.IsAny<string>())).Returns(false);
            
            portMock = new Mock<IPortWatcher>();
            portMock.Setup(m => m.IsPortAvailable(Moq.It.IsAny<int>())).Returns(true);
        };

        Because of = () => runner = MongoDbRunner.StartForUnitTest(processMock.Object, portMock.Object);

        It should_check_for_already_running_process                    = () => processMock.Verify(x => x.IsProcessRunning(MongoDbRunner.MongoDbProcessName), Times.Exactly(1));
        It should_check_the_default_port                               = () => portMock.Verify(x => x.IsPortAvailable(MongoDbRunner.MongoDbDefaultPort), Times.Exactly(1));
        //It should_create_the_data_directory_if_not_exist               = () => false.ShouldBeTrue();
        //It should_delete_old_lock_files                                = () => false.ShouldBeTrue();
        It should_return_an_instance_with_property_running_set_to_true = () => runner.Running.ShouldBeTrue();
    }
}
// ReSharper restore InconsistentNaming