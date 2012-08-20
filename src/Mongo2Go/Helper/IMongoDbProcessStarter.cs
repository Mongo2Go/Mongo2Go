namespace Mongo2Go.Helper
{
    public interface IMongoDbProcessStarter
    {
        IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port);

        IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port, bool doNotKill);
    }
}