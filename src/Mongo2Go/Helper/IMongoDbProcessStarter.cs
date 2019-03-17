namespace Mongo2Go.Helper
{
    public interface IMongoDbProcessStarter
    {
        IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port, bool singleNodeReplSet, string additionalMongodArguments);

        IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port, bool doNotKill, bool singleNodeReplSet, string additionalMongodArguments);
    }
}