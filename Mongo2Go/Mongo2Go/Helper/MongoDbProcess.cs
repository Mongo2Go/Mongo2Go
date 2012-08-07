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

            bool processReady = false;

            Process process = new Process { StartInfo = startInfo };
            process.OutputDataReceived += (sender, args) => { if (args.Data.Contains(ProcessReadyIdentifier))
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


            return new MongoDbProcess(process);
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
