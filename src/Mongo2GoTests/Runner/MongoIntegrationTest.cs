using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mongo2Go;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Mongo2GoTests.Runner
{
    public class MongoIntegrationTest
    {
        internal static MongoDbRunner _runner;
        internal static IMongoCollection<TestDocument> _collection;
        internal static string _databaseName = "IntegrationTest";
        internal static string _collectionName = "TestCollection";

        internal static void CreateConnection()
        {
            _runner = MongoDbRunner.Start();
            
            MongoClient client = new MongoClient(_runner.ConnectionString);
            IMongoDatabase database = client.GetDatabase(_databaseName);
            _collection = database.GetCollection<TestDocument>(_collectionName);
        }

        public static IList<T> ReadBsonFile<T>(string fileName)
        {
            string[] content = File.ReadAllLines(fileName);
            return content.Select(s => BsonSerializer.Deserialize<T>(s)).ToList();
        }
    }
}