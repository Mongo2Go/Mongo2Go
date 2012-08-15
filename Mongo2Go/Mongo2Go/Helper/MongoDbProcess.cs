using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Mongo2Go.Helper
{
    public partial class MongoDbProcess : IMongoDbProcess, IDisposable
    {
        private const string ProcessReadyIdentifier = "waiting for connections";
        private Process _process;
        private bool _doNotKill;

        public IEnumerable<string> StandardOutput { get; set; }

        internal MongoDbProcess(Process process)
        {
            _process = process;
        }

        public IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port)
        {
            return Start(binariesDirectory, dataDirectory, port, false);
        }

        public IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port, bool doNotKill)
        {
            _doNotKill = doNotKill;

            string fileName  = @"{0}\{1}".Formatted(binariesDirectory, MongoDbDefaults.MongodExecutable);
            string arguments = @"--dbpath ""{0}"" --port {1} --nohttpinterface --nojournal".Formatted(dataDirectory, port);

            ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                };

            Process process = new Process { StartInfo = startInfo };

            StandardOutput = ProcessControl.StartAndBlockUntilReady(process, 5, ProcessReadyIdentifier);

            return new MongoDbProcess(process);
        }
    }
}
