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
        internal static MongoCollection<TestDocument> _collection;
        internal static string _databaseName = "IntegrationTest";
        internal static string _collectionName = "TestCollection";

        internal static void CreateConnection()
        {
            _runner = MongoDbRunner.Start();
            
            MongoServer server = MongoServer.Create(_runner.ConnectionString);
            MongoDatabase database = server.GetDatabase(_databaseName);
            _collection = database.GetCollection<TestDocument>(_collectionName);
        }

        public static IList<T> ReadBsonFile<T>(string fileName)
        {
            string[] content = File.ReadAllLines(fileName);
            return content.Select(BsonSerializer.Deserialize<T>).ToList();
        }
    }
}