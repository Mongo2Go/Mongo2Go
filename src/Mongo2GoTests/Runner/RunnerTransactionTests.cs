using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using Machine.Specifications;
using MongoDB.Driver;

namespace Mongo2GoTests.Runner
{
    [Subject("Runner Transaction Test")]
    public class when_transaction_completes : MongoTransactionTest
    {
        private static TestDocument mainDocument;
        private static TestDocument dependentDocument;

        Establish context = () =>

        {
            CreateConnection();
            database.DropCollection(_mainCollectionName);
            database.DropCollection(_dependentCollectionName);
            _mainCollection.InsertOne(TestDocument.DummyData2());
            _dependentCollection.InsertOne(TestDocument.DummyData2());
        };

        private Because of = () =>
        {
            var filter = Builders<TestDocument>.Filter.Where(x => x.IntTest == 23);
            var update = Builders<TestDocument>.Update.Inc(i => i.IntTest, 10);

            using (var sessionHandle = client.StartSession())
            {
                try
                {
                    var i = 0;
                    while (i < 10)
                    {
                        try
                        {
                            i++;
                            sessionHandle.StartTransaction(new TransactionOptions(
                                readConcern: ReadConcern.Local,
                                writeConcern: WriteConcern.W1)); 
                            try
                            {
                                var first = _mainCollection.UpdateOne(sessionHandle, filter, update);
                                var second = _dependentCollection.UpdateOne(sessionHandle, filter, update);
                            }
                            catch (Exception e)
                            {
                                sessionHandle.AbortTransaction();
                                throw;
                            }

                            var j = 0;
                            while (j < 10)
                            {
                                try
                                {
                                    j++;
                                    sessionHandle.CommitTransaction();
                                    break;
                                }
                                catch (MongoException e)
                                {
                                    if (e.HasErrorLabel("UnknownTransactionCommitResult"))
                                        continue;
                                    throw;
                                }
                            }
                            break;
                        }
                        catch (MongoException e)
                        {
                            if (e.HasErrorLabel("TransientTransactionError"))
                                continue;
                            throw;
                        }
                    }
                }
                catch (Exception e)
                {
                    
                }
            }

             mainDocument = _mainCollection.FindSync(Builders<TestDocument>.Filter.Empty).FirstOrDefault();
             dependentDocument = _dependentCollection.FindSync(Builders<TestDocument>.Filter.Empty).FirstOrDefault();
        };
        
        It main_should_be_33 = () => mainDocument.IntTest.Should().Be(33);
        It dependent_should_be_33 = () => dependentDocument.IntTest.Should().Be(33);
        Cleanup cleanup = () => _runner.Dispose();
    }


    [Subject("Runner Transaction Test")]
    public class when_transaction_is_aborted_before_commit : MongoTransactionTest
    {
        private static TestDocument mainDocument;
        private static TestDocument dependentDocument;
        private static TestDocument mainDocument_before_commit;
        private static TestDocument dependentDocument_before_commit;
        Establish context = () =>

        {
            CreateConnection();
            database.DropCollection(_mainCollectionName);
            database.DropCollection(_dependentCollectionName);
            _mainCollection.InsertOne(TestDocument.DummyData2());
            _dependentCollection.InsertOne(TestDocument.DummyData2());

        };

        private Because of = () =>
        {
            var filter = Builders<TestDocument>.Filter.Where(x => x.IntTest == 23);
            var update = Builders<TestDocument>.Update.Inc(i => i.IntTest, 10);
          
            using (var sessionHandle = client.StartSession())
            {
                try
                {
                    var i = 0;
                    while (i < 2)
                    {
                        try
                        {
                            i++;
                            sessionHandle.StartTransaction(new TransactionOptions(
                                readConcern: ReadConcern.Local,
                                writeConcern: WriteConcern.W1));
                            try
                            {
                                var first = _mainCollection.UpdateOne(sessionHandle, filter, update);
                                var second = _dependentCollection.UpdateOne(sessionHandle, filter, update);
                                mainDocument_before_commit = _mainCollection.FindSync(sessionHandle, Builders<TestDocument>.Filter.Empty).ToList().FirstOrDefault();
                                dependentDocument_before_commit = _dependentCollection.FindSync(sessionHandle, Builders<TestDocument>.Filter.Empty).ToList().FirstOrDefault();
                            }
                            catch (Exception e)
                            {
                                sessionHandle.AbortTransaction();
                                throw;
                            }

                            //Throw exception and do not commit
                            throw new ApplicationException();
                        }
                        catch (MongoException e)
                        {
                            if (e.HasErrorLabel("TransientTransactionError"))
                                continue;
                            throw;
                        }

                    }
                }
                catch (Exception e)
                {

                }
            }

            mainDocument = _mainCollection.FindSync(Builders<TestDocument>.Filter.Empty).FirstOrDefault();
            dependentDocument = _dependentCollection.FindSync(Builders<TestDocument>.Filter.Empty).FirstOrDefault();
        };

        It main_should_be_still_23_after_aborting = () => mainDocument.IntTest.Should().Be(23);
        It dependent_should_be_still_23_after_aborting = () => dependentDocument.IntTest.Should().Be(23);
        It main_should_be_33_before_aborting = () => mainDocument_before_commit.IntTest.Should().Be(33);
        It dependent_should_be_33_before_aborting = () => dependentDocument_before_commit.IntTest.Should().Be(33);
        Cleanup cleanup = () => _runner.Dispose();
    }
}
