using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

namespace Mongo2Go.Helper
{
    public class MongoImportExport
    {
        public void Export(string binariesDirectory, int port)
        {
            string fileName = @"{0}\{1}".Formatted(binariesDirectory, MongoDbDefaults.MongodExecutable);
            //string arguments = @"--dbpath ""{0}"" --port {1} --nohttpinterface --nojournal".Formatted(dataDirectory, port);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                //Arguments = arguments,
                //CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
            };

            Process process = new Process { StartInfo = startInfo };

        }
    }
}
