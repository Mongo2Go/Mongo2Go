using Machine.Specifications;
using Mongo2Go;
using Mongo2Go.Helper;
using Moq;
using It = Machine.Specifications.It;

// ReSharper disable InconsistentNaming
namespace Mongo2GoTests.Runner
{
    //[Ignore("Intergration Test should be started by hand")]
    [Subject("Runner Integration Test")]
    public class when_instanciating_a_real_runner
    {
        private static MongoDbRunner runner;

        Establish context = () =>
            {
                runner = MongoDbRunner.Start();
            };

        private Because of = () => runner.ToString();

        It should_return_an_instance_with_state_running  = () => runner.State.ShouldEqual(State.Running);
    }
}
// ReSharper restore InconsistentNaming