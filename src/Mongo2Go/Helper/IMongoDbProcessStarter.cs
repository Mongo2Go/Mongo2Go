using Microsoft.Extensions.Logging;

namespace Mongo2Go.Helper
{
    public interface IMongoDbProcessStarter
    {
        IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port, bool singleNodeReplSet, string additionalMongodArguments, ushort singleNodeReplSetWaitTimeout = MongoDbDefaults.SingleNodeReplicaSetWaitTimeout, ILogger logger = null);

        IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port, bool doNotKill, bool singleNodeReplSet, string additionalMongodArguments, ushort singleNodeReplSetWaitTimeout = MongoDbDefaults.SingleNodeReplicaSetWaitTimeout, ILogger logger = null);
    }
}