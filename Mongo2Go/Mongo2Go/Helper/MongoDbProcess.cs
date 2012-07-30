using System.Diagnostics;

namespace Mongo2Go.Helper
{
    public class MongoDbProcess : IMongoDbProcess
    {
        private Process _process;

        public MongoDbProcess(string binariesFolder)
        {
            string fileName = binariesFolder + @"\" + MongoDbDefaults.ProcessName;

            ProcessStartInfo startInfo = new ProcessStartInfo(fileName)
                {
                    WorkingDirectory = binariesFolder,
                    //CreateNoWindow = true,
                    //UseShellExecute = false
                };

            _process = Process.Start(startInfo);
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
    }
}
