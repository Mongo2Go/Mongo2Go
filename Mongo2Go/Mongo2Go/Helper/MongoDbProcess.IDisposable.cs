using System;
using System.Threading;

namespace Mongo2Go.Helper
{
    //  IDisposable and friends
    public partial class MongoDbProcess
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Disposed) { return; }
            if (disposing)
            {
                // we have no "managed resources" - but we leave this switch to avoid an FxCop CA1801 warnig
            }
            Kill();

            Disposed = true;
        }

        ~MongoDbProcess()
        {
            Dispose(false);
        }

        private void Kill()
        {
            if (_doNotKill)
            {
                // nothing to do
                return;
            }

            if (_process == null)
            {
                return;
            }

            if (!_process.HasExited)
            {
                _process.Kill();
            }

            _process.Dispose();
            _process = null;

            // wait a bit to be sure
            Thread.Sleep(500);
        }
    }
}
