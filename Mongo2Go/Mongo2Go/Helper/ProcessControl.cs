using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;

namespace Mongo2Go.Helper
{
    public static class ProcessControl
    {
        /// <summary>
        /// Reads from Output stream to determine if prozess is ready
        /// </summary>
        public static IEnumerable<string> StartAndBlockUntilReady(Process process, int timeoutInSeconds, string processReadyIdentifier)
        {
            if (timeoutInSeconds < 1 ||
                timeoutInSeconds > 20)
            {
                 throw new ArgumentOutOfRangeException("timeoutInSeconds", "The amount in seconds should have a value between 1 and 20.");
            }

            List<string> standardOutput = new List<string>(); 

            bool processReady = false;
            process.OutputDataReceived += (sender, args) =>
            {
                standardOutput.Add(args.Data);

                if (!string.IsNullOrEmpty(args.Data) &&
                    args.Data.Contains(processReadyIdentifier))
                {
                    processReady = true;
                }
            };

            process.Start();
            process.BeginOutputReadLine();

            int lastResortCounter = 0;
            int timeOut = timeoutInSeconds * 10;
            while (!processReady)
            {
                Thread.Sleep(100);
                if (++lastResortCounter > timeOut)
                {
                    // we waited X seconds.
                    // for any reason the detection did not worked, eg. the identifier changed
                    // lets assume everything is still ok
                    break;
                }
            }

            process.CancelOutputRead();

            return standardOutput;
        }

    }
}
