using System;

namespace Mongo2Go
{
    //  IDisposable and friends
    public partial class MongoDbRunner
    {
        ~MongoDbRunner()
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

            if (State != State.Running)
            {
                return;
            }

            if (disposing)
            {
                // we have no "managed resources" - but we leave this switch to avoid an FxCop CA1801 warnig
            }

            if (_process != null)
            {
                _process.Dispose();
            }

            // will be null if we are working in debugging mode (single instance)
            if (_dataDirectoryWithPort != null)
            {
                // finally clean up the data directory we created previously
                _fileSystem.DeleteFolder(_dataDirectoryWithPort);
            }

            Disposed = true;
            State = State.Stopped;
        }
    }
}
