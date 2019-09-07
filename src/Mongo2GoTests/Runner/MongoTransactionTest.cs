using System;
using System.Collections.Generic;
using System.Text;
using Mongo2Go;
using MongoDB.Driver;

namespace Mongo2GoTests.Runner
{
    public class MongoTransactionTest
    {
        internal static MongoDbRunner _runner;
        internal static IMongoCollection<TestDocument> _mainCollection;
        internal static IMongoCollection<TestDocument> _dependentCollection;
        internal static string _databaseName = "TransactionTest";
        internal static string _mainCollectionName = "MainCollection";
        internal static string _dependentCollectionName = "DependentCollection";
        internal static IMongoDatabase database;
        internal static IMongoClient client;
        internal static void CreateConnection(ushort? singleNodeReplSetWaitTimeout = null)
        {
            if (singleNodeReplSetWaitTimeout.HasValue)
            {
                _runner = MongoDbRunner.Start(singleNodeReplSet: true, singleNodeReplSetWaitTimeout: singleNodeReplSetWaitTimeout.Value);
            }
            else
            {
                _runner = MongoDbRunner.Start(singleNodeReplSet: true);
            }

            client = new MongoClient(_runner.ConnectionString);
            database = client.GetDatabase(_databaseName);
            _mainCollection = database.GetCollection<TestDocument>(_mainCollectionName);
            _dependentCollection = database.GetCollection<TestDocument>(_dependentCollectionName);
        }
    }
}
