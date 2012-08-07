using System;
using System.Runtime.Serialization;

namespace Mongo2Go
{
    [Serializable]
    public class NoFreePortFoundException : Exception
    {
        public NoFreePortFoundException() { }
        public NoFreePortFoundException(string message) : base(message) { }
        public NoFreePortFoundException(string message, Exception inner) : base(message, inner) { }
        protected NoFreePortFoundException( SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}