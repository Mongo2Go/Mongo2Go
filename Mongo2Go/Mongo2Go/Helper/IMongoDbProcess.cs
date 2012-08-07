using System;

namespace Mongo2Go.Helper
{
    public interface IMongoDbProcess : IDisposable
    {
        IMongoDbProcess Start(string binariesFolder);
    }
}