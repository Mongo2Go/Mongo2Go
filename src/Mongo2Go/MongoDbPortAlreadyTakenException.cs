using System;
using System.Runtime.Serialization;

namespace Mongo2Go
{
    [Serializable]
    public class MongoDbPortAlreadyTakenException : Exception
    {
        public MongoDbPortAlreadyTakenException() { }
        public MongoDbPortAlreadyTakenException(string message) : base(message) { }
        public MongoDbPortAlreadyTakenException(string message, Exception inner) : base(message, inner) { }
        protected MongoDbPortAlreadyTakenException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}