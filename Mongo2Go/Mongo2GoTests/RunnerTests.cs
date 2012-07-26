using Machine.Specifications;
using Mongo2Go;
using Mongo2Go.Helper;
using Moq;
using It = Machine.Specifications.It;
using MoqIt = Moq.It;

// ReSharper disable InconsistentNaming
namespace Mongo2GoTests
{
    [Subject("Mongo2Go")]
    public class when_instanciating_the_runner
    {
        private static MongoDbRunner runner;
        private static IPortWatcher portWatcher;
        private static Mock<IPortWatcher> portMock;

        Establish context = () =>
        {
            portMock = new Mock<IPortWatcher>();
            portMock.Setup(m => m.IsPortAvailable(MoqIt.IsAny<int>())).Returns(true);
        };

        Because of = () => runner = MongoDbRunner.StartForUnitTest(portMock.Object);

        It should_check_for_already_running_process = () => false.ShouldBeTrue();
        It should_check_the_default_port = () => portMock.Verify(x => x.IsPortAvailable(MongoDbRunner.MongoDbDefaultPort), Times.Exactly(1));
        It should_create_the_data_directory_if_not_exist = () => false.ShouldBeTrue();
        It should_delete_old_lock_files = () => false.ShouldBeTrue();
        It should_return_an_instance_with_property_running_set_to_true = () => runner.Running.ShouldBeTrue();
    }
}
// ReSharper restore InconsistentNaming