using Machine.Specifications;
using Mongo2Go;
using It = Machine.Specifications.It;

// ReSharper disable InconsistentNaming
namespace Mongo2GoTests
{
    [Subject("Mongo2Go")]
    public class when_instanciating_the_runner
    {
        private static MongoDbRunner runner;

        private Because of = () => runner = MongoDbRunner.Start();

        private It should_check_for_already_running_process = () => false.ShouldBeTrue();
        private It should_check_the_default_port = () => false.ShouldBeTrue();
        private It should_create_the_data_directory_if_not_exist = () => false.ShouldBeTrue();
        private It should_delete_old_lock_files = () => false.ShouldBeTrue();
        private It should_return_an_instance_with_property_running_set_to_true = () => runner.Running.ShouldBeTrue();
    }
}
// ReSharper restore InconsistentNaming