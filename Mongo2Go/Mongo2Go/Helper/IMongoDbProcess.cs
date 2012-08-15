using System;
using System.Collections.Generic;

namespace Mongo2Go.Helper
{
    public interface IMongoDbProcess : IDisposable
    {
        IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port);
        IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port, bool doNotKill);
        IEnumerable<string> StandardOutput { get; }
    }
}