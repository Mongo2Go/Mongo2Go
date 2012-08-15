using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace Mongo2Go.Helper
{
    public static class ProcessControl
    {
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool SetWindowText(IntPtr hwnd, String lpString);

        public static Process ProcessFactory(string fileName, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = false,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            Process process = new Process { StartInfo = startInfo };
            return process;
        }

        public static ProcessOutput StartAndWaitForExit(Process process, string windowTitle)
        {
            List<string> errorOutput = new List<string>();
            List<string> standardOutput = new List<string>();
            
            process.ErrorDataReceived  += (sender, args) => errorOutput.Add(args.Data);
            process.OutputDataReceived += (sender, args) => standardOutput.Add(args.Data);

            process.Start();

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            RenameWindow(process, windowTitle);
            process.WaitForExit();

            process.CancelErrorRead();
            process.CancelOutputRead();

            return new ProcessOutput(errorOutput, standardOutput);
        }


        public static void RenameWindow(Process process, string newName)
        {
            if (String.IsNullOrEmpty(newName))
            {
                return;
            }

            try {
                SetWindowText(process.MainWindowHandle, newName);
            } 
            catch (Exception ex) { }
        }

        /// <summary>
        /// Reads from Output stream to determine if prozess is ready
        /// </summary>
        public static ProcessOutput StartAndWaitForReady(Process process, int timeoutInSeconds, string processReadyIdentifier, string windowTitle)
        {
            if (timeoutInSeconds < 1 ||
                timeoutInSeconds > 10)
            {
                 throw new ArgumentOutOfRangeException("timeoutInSeconds", "The amount in seconds should have a value between 1 and 10.");
            }
            
            List<string> errorOutput = new List<string>();
            List<string> standardOutput = new List<string>();
            bool processReady = false;

            
            process.ErrorDataReceived  += (sender, args) => errorOutput.Add(args.Data);
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

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            // slows down the tests but magically solves all problems when doing integration tests on different ports
            Thread.Sleep(100);

            RenameWindow(process, windowTitle);

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

            process.CancelErrorRead();
            process.CancelOutputRead();

            return new ProcessOutput(errorOutput, standardOutput);
        }
    }
}
