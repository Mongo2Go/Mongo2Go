using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Mongo2Go.Helper
{
    public class MongoDbProcess : IMongoDbProcess
    {
        private const string ProcessReadyIdentifier = "waiting for connections";
        private Process _process;

        internal MongoDbProcess(Process process)
        {
            _process = process;
        }

        public IMongoDbProcess Start(string binariesFolder)
        {
            string fileName = string.Format(CultureInfo.InvariantCulture, @"{0}\{1}", binariesFolder, MongoDbDefaults.ProcessName);
            string arguments = string.Format(CultureInfo.InvariantCulture, @"--dbpath ""{0}"" --port {1} --nohttpinterface --nojournal", MongoDbDefaults.DataFolder, MongoDbDefaults.Port);

            ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    //CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                };

            Process process = new Process { StartInfo = startInfo };

            BlockUntilStarted(process);

            return new MongoDbProcess(process);
        }

        private static void BlockUntilStarted(Process process)
        {
            bool processReady = false;
            process.OutputDataReceived += (sender, args) =>
                {
                    if (!string.IsNullOrEmpty(args.Data) &&
                        args.Data.Contains(ProcessReadyIdentifier))
                    {
                        processReady = true;
                    }
                };

            process.Start();
            process.BeginOutputReadLine();

            while (!processReady)
            {
                Thread.Sleep(100);
            }

            process.CancelOutputRead();
        }

        public void Kill()
        {
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
        }

        ~MongoDbProcess()
        {
           Kill(); 
        }
    }
}
