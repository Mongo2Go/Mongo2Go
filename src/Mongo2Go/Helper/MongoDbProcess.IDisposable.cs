using System;

namespace Mongo2Go.Helper
{
    //  IDisposable and friends
    public partial class MongoDbProcess
    {
        ~MongoDbProcess()
        {
            Dispose(false);
        }

        public bool Disposed { get; private set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (Disposed)
            {
                return;
            }

            if (disposing)
            {
                // we have no "managed resources" - but we leave this switch to avoid an FxCop CA1801 warnig
            }

            if (_process == null)
            {
                return;
            }

            if (_process.DoNotKill)
            {
                // StartForDebugging leaves mongod running on purpose, and its stdout/stderr pipes must
                // stay open or the surviving process hits a broken pipe - so we deliberately do NOT dispose
                // the process here. Mark Disposed so the finalizer does not run this logic again (#147).
                Disposed = true;
                return;
            }

            if (!_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit();
            }

            _process.Dispose();
            _process = null;

            Disposed = true;
        }
    }
}
