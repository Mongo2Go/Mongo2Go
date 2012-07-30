using System;
using System.Runtime.Serialization;

namespace Mongo2Go
{
    [Serializable]
    public class MonogDbBinariesNotFoundException : Exception
    {
        public MonogDbBinariesNotFoundException() { }
        public MonogDbBinariesNotFoundException(string message) : base(message) { }
        public MonogDbBinariesNotFoundException(string message, Exception inner) : base(message, inner) { }
        protected MonogDbBinariesNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}