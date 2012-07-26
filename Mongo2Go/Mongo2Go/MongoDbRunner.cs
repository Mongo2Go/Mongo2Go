using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Mongo2Go
{
    // TODO: support of multiple MongoDB instances

    public class MongoDbRunner : IDisposable
    {
        public bool Running { get; private set; }

        private MongoDbRunner() { }
        
        public static MongoDbRunner Start()
        {
            return new MongoDbRunner
                       {
                           Running = true
                       };
        }

        #region IDisposable

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Running && disposing)
            {
                
            }
        }

        #endregion
    }
}
