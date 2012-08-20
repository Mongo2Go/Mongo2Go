using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Mongo2Go.Helper
{
    public partial class MongoDbProcess : IMongoDbProcess, IDisposable
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
