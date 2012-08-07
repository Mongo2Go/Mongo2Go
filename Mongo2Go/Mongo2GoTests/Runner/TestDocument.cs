using System;
using System.Collections.Generic;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Mongo2GoTests.Runner
{
    public class TestDocument
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; }

        public string StringTest { get; set; }

        public int IntTest { get; set; }
        
        [BsonDateTimeOptions(Kind = DateTimeKind.Local)]
        public DateTime DateTest { get; set; }

        public List<string> ListTest { get; set; }

        public static TestDocument DummyData1()
        {
            return new TestDocument
                {
                    StringTest = "Hello World",
                    IntTest = 42,
                    DateTest = new DateTime(1984, 09, 30, 6, 6, 6),
                    ListTest = new List<string> {"I", "am", "a", "list", "of", "strings"}
                };
        }

        public static TestDocument DummyData2()
        {
            return new TestDocument
            {
                StringTest = "Foo",
                IntTest = 23,
            };
        }

        public static TestDocument DummyData3()
        {
            return new TestDocument
            {
                StringTest = "Bar",
                IntTest = 77,
            };
        }

    }
}
