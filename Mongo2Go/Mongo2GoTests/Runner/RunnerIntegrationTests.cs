using FluentAssertions;
using Machine.Specifications;
using Mongo2Go;
using Mongo2Go.Helper;
using MongoDB.Bson;
using MongoDB.Driver;
using Moq;
using It = Machine.Specifications.It;

// ReSharper disable InconsistentNaming
namespace Mongo2GoTests.Runner
{
    //[Ignore("Intergration Test should be started by hand")]
    [Subject("Runner Integration Test")]
    public class when_instanciating_a_real_runner : MongoIntegrationTest
    {
        static TestDocument findResult;
        
        Establish context = () =>
            {
                runner = MongoDbRunner.Start();
                CreateConnection();
                collection.Insert(TestDocument.DummyData());
            };

        Because of = () => findResult = collection.FindOneAs<TestDocument>();

        It should_return_a_result = () => findResult.ShouldNotBeNull();
        It should_hava_expected_data = () => findResult.ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData());
    }

    public class MongoIntegrationTest
    {
        internal static MongoDbRunner runner;
        internal static MongoCollection<BsonDocument> collection;

        internal static void CreateConnection()
        {
            const string connectionString = "mongodb://localhost/?safe=true";
            MongoServer server = MongoServer.Create(connectionString);
            MongoDatabase database = server.GetDatabase("IntegrationTest");
            collection = database.GetCollection("TestCollection");
        }
    }
}
// ReSharper restore InconsistentNaming