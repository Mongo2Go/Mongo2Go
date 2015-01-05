using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Machine.Specifications;
using MongoDB.Driver.Linq;
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
                _collection.Insert(TestDocument.DummyData1());
            };

        Because of = () => findResult = _collection.FindOneAs<TestDocument>();

        It should_return_a_result = () => findResult.Should().NotBeNull();
        It should_hava_expected_data = () => findResult.ShouldBeEquivalentTo(TestDocument.DummyData1(), cfg => cfg.Excluding(d => d.Id));

        Cleanup stuff = () => _runner.Dispose();
    }

    [Subject("Runner Integration Test")]
    public class when_using_the_new_linq_support : MongoIntegrationTest
    {
        static List<TestDocument> queryResult;

        Establish context = () =>
        {
            CreateConnection();
            _collection.Insert(TestDocument.DummyData1());
            _collection.Insert(TestDocument.DummyData2());
            _collection.Insert(TestDocument.DummyData3());
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
}
// ReSharper restore UnusedMember.Local
// ReSharper restore InconsistentNaming