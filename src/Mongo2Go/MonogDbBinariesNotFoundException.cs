using System;

namespace Mongo2Go
{
    public class MonogDbBinariesNotFoundException : Exception
    {
        public MonogDbBinariesNotFoundException() { }
        public MonogDbBinariesNotFoundException(string message) : base(message) { }
        public MonogDbBinariesNotFoundException(string message, Exception inner) : base(message, inner) { }
    }
}