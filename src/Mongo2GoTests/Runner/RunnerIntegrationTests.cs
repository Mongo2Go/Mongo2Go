using FluentAssertions;
using Machine.Specifications;
using MELT;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using It = Machine.Specifications.It;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
namespace Mongo2GoTests.Runner
{
    [Subject("Runner Integration Test")]
    public class when_using_the_inbuild_serialization : MongoIntegrationTest
    {
        static TestDocument findResult;


        Establish context = () =>
        {
            CreateConnection();
            _collection.InsertOne(TestDocument.DummyData1());
        };

        Because of = () => findResult = _collection.FindSync<TestDocument>(_ => true).First();

        It should_return_a_result = () => findResult.Should().NotBeNull();
        It should_hava_expected_data = () => findResult.Should().BeEquivalentTo(TestDocument.DummyData1(), cfg => cfg.Excluding(d => d.Id));

        Cleanup stuff = () => _runner.Dispose();
    }

    [Subject("Runner Integration Test")]
    public class when_using_the_new_linq_support : MongoIntegrationTest
    {
        static List<TestDocument> queryResult;

        Establish context = () =>
        {
            CreateConnection();
            _collection.InsertOne(TestDocument.DummyData1());
            _collection.InsertOne(TestDocument.DummyData2());
            _collection.InsertOne(TestDocument.DummyData3());
        };

        Because of = () =>
        {
            queryResult = (from c in _collection.AsQueryable()
                           where c.StringTest == TestDocument.DummyData2().StringTest || c.StringTest == TestDocument.DummyData3().StringTest
                           select c).ToList();
        };

        It should_return_two_documents = () => queryResult.Count().Should().Be(2);
        It should_return_document2 = () => queryResult.ElementAt(0).IntTest = TestDocument.DummyData2().IntTest;
        It should_return_document3 = () => queryResult.ElementAt(1).IntTest = TestDocument.DummyData3().IntTest;

        Cleanup stuff = () => _runner.Dispose();
    }

    [Subject("Runner Integration Test")]
    public class when_using_commands_that_create_console_output : MongoIntegrationTest
    {
        static List<Task> taskList = new List<Task>();

        private Establish context = () =>
        {
            CreateConnection();
        };

        private Because of = () =>
        {
            var createIndexModel = new CreateIndexModel<TestDocument>(Builders<TestDocument>.IndexKeys.Ascending(x => x.IntTest));
            taskList.Add(_collection.Indexes.CreateOneAsync(createIndexModel).WithTimeout(TimeSpan.FromMilliseconds(5000)));
            taskList.Add(_collection.Indexes.DropAllAsync().WithTimeout(TimeSpan.FromMilliseconds(5000)));
        };

        It should_not_timeout = () => Task.WaitAll(taskList.ToArray());

        Cleanup stuff = () => _runner.Dispose();
    }


    [Subject("Runner Integration Test")]
    public class when_using_microsoft_ilogger : MongoIntegrationTest
    {
        static List<Task> taskList = new List<Task>();
        static ITestLoggerFactory loggerFactory;

        private Establish context = () =>
        {
            loggerFactory = TestLoggerFactory.Create();
            var logger = loggerFactory.CreateLogger("MyTestLogger");
            CreateConnection(logger);
        };

        private Because of = () =>
        {
            var createIndexModel = new CreateIndexModel<TestDocument>(Builders<TestDocument>.IndexKeys.Ascending(x => x.IntTest));
            taskList.Add(_collection.Indexes.CreateOneAsync(createIndexModel).WithTimeout(TimeSpan.FromMilliseconds(5000)));
            taskList.Add(_collection.Indexes.DropAllAsync().WithTimeout(TimeSpan.FromMilliseconds(5000)));
        };

        It should_not_timeout = () => Task.WaitAll(taskList.ToArray());
        It should_have_received_many_logs = () =>
            loggerFactory.Sink.LogEntries.Count(l => l.LogLevel == Microsoft.Extensions.Logging.LogLevel.Information)
            .Should().BeGreaterThan(10);
        It should_have_created_collection_statement = () => loggerFactory.Sink.LogEntries
            .Count(l => l.Properties.Any(p => p.Key == "message" && (string)p.Value == "createCollection"))
            .Should().BeGreaterOrEqualTo(1);

        Cleanup stuff = () => _runner.Dispose();
    }
}
// ReSharper restore UnusedMember.Local
// ReSharper restore InconsistentNaming