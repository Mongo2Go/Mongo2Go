using System;
using System.Collections.Generic;

namespace Mongo2Go.Helper
{
    public interface IMongoDbProcess : IDisposable
    {
        IEnumerable<string> StandardOutput { get; }
        IEnumerable<string> ErrorOutput { get; } 
    }
}