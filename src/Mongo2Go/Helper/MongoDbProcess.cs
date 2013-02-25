using System.Collections.Generic;

namespace Mongo2Go.Helper
{
    public partial class MongoDbProcess : IMongoDbProcess
    {

        private WrappedProcess _process;

        public IEnumerable<string> ErrorOutput { get; set; }
        public IEnumerable<string> StandardOutput { get; set; }

        internal MongoDbProcess(WrappedProcess process)
        {
            _process = process;
        }

    }
}
