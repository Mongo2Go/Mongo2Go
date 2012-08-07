namespace Mongo2Go.Helper
{
    public interface IMongoDbProcess
    {
        IMongoDbProcess Start(string binariesFolder);
        void Kill();
    }
}