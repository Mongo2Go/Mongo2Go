using System;

namespace Mongo2Go
{
    public class MongoDbPortAlreadyTakenException : Exception
    {
        public MongoDbPortAlreadyTakenException() { }
        public MongoDbPortAlreadyTakenException(string message) : base(message) { }
        public MongoDbPortAlreadyTakenException(string message, Exception inner) : base(message, inner) { }
    }
}