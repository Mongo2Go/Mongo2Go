namespace Mongo2Go
{
    public static class MongoDbDefaults
    {
        public const string ExecutableName = "mongod";

        // 27017 is the default port, but we don't want to get in trouble with productive systems
        public const int Port = 27018;

        public const string DataDirectory = @"C:\data\db";

        public const string Lockfile = "mongod.lock";
    }
}
