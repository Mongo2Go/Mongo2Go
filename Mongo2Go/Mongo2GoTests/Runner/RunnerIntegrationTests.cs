using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Machine.Specifications;
using Mongo2Go;
using Mongo2Go.Helper;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using MongoDB.Driver.Linq;
using Newtonsoft.Json;
using It = Machine.Specifications.It;

// ReSharper disable InconsistentNaming
namespace Mongo2GoTests.Runner
{
    [Subject("Runner Integration Test")]
    public class when_using_the_inbuild_serialization : MongoIntegrationTest
    {
        static TestDocument findResult;
        
        Establish context = () =>
            {
                runner = MongoDbRunner.Start();
                CreateConnection();
                collection.Insert(TestDocument.DummyData1());
            };

        Because of = () => findResult = collection.FindOneAs<TestDocument>();

        It should_return_a_result = () => findResult.ShouldNotBeNull();
        It should_hava_expected_data = () => findResult.ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData1());
    }

    [Subject("Runner Integration Test")]
    public class when_using_the_new_linq_support : MongoIntegrationTest
    {
        static IQueryable<TestDocument> query;

        Establish context = () =>
        {
            CreateConnection();
            collection.Insert(TestDocument.DummyData1());
            collection.Insert(TestDocument.DummyData2());
            collection.Insert(TestDocument.DummyData3());
        };

        Because of = () =>
            {
                query = from c in collection.AsQueryable()
                            where c.StringTest == TestDocument.DummyData2().StringTest || c.StringTest == TestDocument.DummyData3().StringTest
                            select c;
                };

        It should_return_two_documents = () => query.Count().ShouldEqual(2);
        It should_return_document2 = () => query.ElementAt(0).IntTest = TestDocument.DummyData2().IntTest;
        It should_return_document3 = () => query.ElementAt(1).IntTest = TestDocument.DummyData3().IntTest;
    }

    [Subject("Runner Integration Test")]
    public class when_using_monogoexport: MongoIntegrationTest
    {
        const string _testFile = @"C:\test.json";
        static IList<TestDocument> parsedContent;

        Establish context = () =>
        {
            CreateConnection();
            collection.Insert(TestDocument.DummyData1());
            collection.Insert(TestDocument.DummyData2());
            collection.Insert(TestDocument.DummyData3());
        };

        Because of = () =>
            {
                runner.Export(_databaseName, _collectionName, _testFile);
                parsedContent = ReadBsonFile<TestDocument>(_testFile);
            };

        It should_preserve_all_values1 = () => parsedContent[0].ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData1());
        It should_preserve_all_values2 = () => parsedContent[1].ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData2());
        It should_preserve_all_values3 = () => parsedContent[2].ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData3());

        //Cleanup stuff = () => new FileSystem().DeleteFile(_testFile);
    }

    public class MongoIntegrationTest
    {
        internal static MongoDbRunner runner;
        internal static MongoCollection<TestDocument> collection;
        internal static string _databaseName = "IntegrationTest";
        internal static string _collectionName = "TestCollection";

        internal static void CreateConnection()
        {
            runner = MongoDbRunner.Start();
            
            MongoServer server = MongoServer.Create(runner.ConnectionString);
            MongoDatabase database = server.GetDatabase(_databaseName);
            collection = database.GetCollection<TestDocument>(_collectionName);
        }

        Cleanup stuff = () => runner.Dispose();

        public static IList<T> ReadBsonFile<T>(string fileName)
        {
            string[] exportedContent = File.ReadAllLines(fileName);

            List<T> tmp = new List<T>();
            foreach(string oneDoc in exportedContent)
            {
                T theDoc = BsonSerializer.Deserialize<T>(oneDoc);
                tmp.Add(theDoc);
            }

            return tmp;


            //return exportedContent.Select(BsonSerializer.Deserialize<T>).ToList();
        }
    }
}
// ReSharper restore InconsistentNaming