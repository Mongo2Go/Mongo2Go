using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mongo2Go;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Mongo2GoTests.Runner
{
    public class MongoDebuggingTest
    {
        internal static MongoDbRunner _runner;
        internal static IMongoCollection<TestDocument> _collection;
        internal static string _databaseName = "IntegrationTest";
        internal static string _collectionName = "TestCollection";
        internal static IMongoDatabase _database;

        internal static void CreateConnection()
        {
            _runner = MongoDbRunner.StartForDebugging(singleNodeReplSet: false);

            MongoClient client = new MongoClient(_runner.ConnectionString);
            _database = client.GetDatabase(_databaseName);
            _collection = _database.GetCollection<TestDocument>(_collectionName);
        }

        public static IList<T> ReadBsonFile<T>(string fileName)
        {
            string[] content = File.ReadAllLines(fileName);
            return content.Select(s => BsonSerializer.Deserialize<T>(s)).ToList();
        }
    }
}