using System.Diagnostics;

namespace Mongo2Go.Helper
{
    public class MongoDbProcess : Process
    {
        public MongoDbProcess(string binariesFolder)
        {

            StartInfo.FileName = binariesFolder + @"\" + MongoDbDefaults.ProcessName;
            StartInfo.WorkingDirectory = binariesFolder;
            StartInfo.CreateNoWindow = true;
            StartInfo.UseShellExecute = false;

            Start();
        }

        public void Shutdown()
        {
            if (!HasExited)
            {
                Kill();
            }
            Dispose();
        }
    }
}
