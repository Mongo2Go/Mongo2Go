using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using FluentAssertions;
using Machine.Specifications;
using Mongo2Go.Helper;
using MongoDB.Driver.Linq;
using It = Machine.Specifications.It;

// ReSharper disable InconsistentNaming
// ReSharper disable UnusedMember.Local
namespace Mongo2GoTests.Runner
{
    [Subject("Runner Integration Test")]
    public class when_using_monogoexport : MongoDebuggingTest
    {
        const string _testFile = @"C:\testExport.json";
        static IList<TestDocument> parsedContent;

        Establish context = () =>

        {
            CreateConnection();
            _collection.Drop();

            _collection.Insert(TestDocument.DummyData1());
            _collection.Insert(TestDocument.DummyData2());
            _collection.Insert(TestDocument.DummyData3());
        };

        Because of = () =>
            {
                _runner.Export(_databaseName, _collectionName, _testFile);
                Thread.Sleep(500); 
                parsedContent = ReadBsonFile<TestDocument>(_testFile);
            };

        It should_preserve_all_values1 = () => parsedContent[0].ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData1());
        It should_preserve_all_values2 = () => parsedContent[1].ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData2());
        It should_preserve_all_values3 = () => parsedContent[2].ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData3());

        Cleanup stuff = () =>
            {
                new FileSystem().DeleteFile(_testFile);
                _runner.Dispose();
            };
    }

    [Subject("Runner Integration Test")]
    public class when_using_monogoimport : MongoDebuggingTest
    {
        static IQueryable<TestDocument> query;
        const string _testFile = @"C:\testImport.json";

        const string _filecontent =
            @"{ ""_id"" : { ""$oid"" : ""50227b375dff9218248eadc4"" }, ""StringTest"" : ""Hello World"", ""IntTest"" : 42, ""DateTest"" : { ""$date"" : 465365166000 }, ""ListTest"" : [ ""I"", ""am"", ""a"", ""list"", ""of"", ""strings"" ] }" + "\r\n" +
            @"{ ""_id"" : { ""$oid"" : ""50227b375dff9218248eadc5"" }, ""StringTest"" : ""Foo"", ""IntTest"" : 23, ""DateTest"" : null, ""ListTest"" : null }" + "\r\n" +
            @"{ ""_id"" : { ""$oid"" : ""50227b375dff9218248eadc6"" }, ""StringTest"" : ""Bar"", ""IntTest"" : 77, ""DateTest"" : null, ""ListTest"" : null }" + "\r\n";

        Establish context = () =>
            {
                CreateConnection();
                _collection.Drop();
                File.WriteAllText(_testFile, _filecontent);
            };

        Because of = () =>
            {
                _runner.Import(_databaseName, _collectionName, _testFile, true);
                Thread.Sleep(500);
                query = _collection.AsQueryable().Select(c => c);

            };

        It should_return_document1 = () => query.ElementAt(0).ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData1());
        It should_return_document2 = () => query.ElementAt(1).ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData2());
        It should_return_document3 = () => query.ElementAt(2).ShouldHave().AllPropertiesBut(d => d.Id).EqualTo(TestDocument.DummyData3());
        
        Cleanup stuff = () =>
            {
                new FileSystem().DeleteFile(_testFile);
                _runner.Dispose();
            };
    }
}
// ReSharper restore UnusedMember.Local
// ReSharper restore InconsistentNaming