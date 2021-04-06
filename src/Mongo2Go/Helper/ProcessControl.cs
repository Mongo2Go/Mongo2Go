using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;

namespace Mongo2Go.Helper
{
    public static class ProcessControl
    {
        public static WrappedProcess ProcessFactory(string fileName, string arguments)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            WrappedProcess process = new WrappedProcess { StartInfo = startInfo };
            return process;
        }

        public static ProcessOutput StartAndWaitForExit(Process process)
        {
            List<string> errorOutput = new List<string>();
            List<string> standardOutput = new List<string>();

            process.ErrorDataReceived += (sender, args) => errorOutput.Add(args.Data);
            process.OutputDataReceived += (sender, args) => standardOutput.Add(args.Data);

            process.Start();

            process.BeginErrorReadLine();
            process.BeginOutputReadLine();

            process.WaitForExit();

            process.CancelErrorRead();
            process.CancelOutputRead();

            return new ProcessOutput(errorOutput, standardOutput);
        }

        /// <summary>
        /// Reads from Output stream to determine if process is ready
        /// </summary>
        public static ProcessOutput StartAndWaitForReady(Process process, int timeoutInSeconds, string processReadyIdentifier, ILogger logger = null)
        {
            if (timeoutInSeconds < 1 ||
                timeoutInSeconds > 10)
            {
                throw new ArgumentOutOfRangeException("timeoutInSeconds", "The amount in seconds should have a value between 1 and 10.");
            }

            // Determine when the process is ready, and store the error and standard outputs
            // to eventually return them.
            List<string> errorOutput = new List<string>();
            List<string> standardOutput = new List<string>();
            bool processReady = false;

            void OnProcessOnErrorDataReceived(object sender, DataReceivedEventArgs args) => errorOutput.Add(args.Data);
            void OnProcessOnOutputDataReceived(object sender, DataReceivedEventArgs args)
            {
                standardOutput.Add(args.Data);

                if (!string.IsNullOrEmpty(args.Data) && args.Data.Contains(processReadyIdentifier, StringComparison.OrdinalIgnoreCase))
                {
                    processReady = true;
                }
            }

            process.ErrorDataReceived += OnProcessOnErrorDataReceived;
            process.OutputDataReceived += OnProcessOnOutputDataReceived;

            if (logger == null)
                WireLogsToConsoleAndDebugOutput(process);
            else
                WireLogsToLogger(process, logger);

            process.Start();

            process.BeginErrorReadLine();
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

            //unsubscribing writing to list - to prevent memory overflow.
            process.ErrorDataReceived -= OnProcessOnErrorDataReceived;
            process.OutputDataReceived -= OnProcessOnOutputDataReceived;

            return new ProcessOutput(errorOutput, standardOutput);
        }

        /// <summary>
        /// Send the mongod process logs to .NET's console and debug outputs.
        /// </summary>
        /// <param name="process"></param>
        private static void WireLogsToConsoleAndDebugOutput(Process process)
        {
            void DebugOutputHandler(object sender, DataReceivedEventArgs args) => Debug.WriteLine(args.Data);
            void ConsoleOutputHandler(object sender, DataReceivedEventArgs args) => Console.WriteLine(args.Data);

            //Writing to debug trace & console to enable test runners to capture the output
            process.ErrorDataReceived += DebugOutputHandler;
            process.ErrorDataReceived += ConsoleOutputHandler;
            process.OutputDataReceived += DebugOutputHandler;
            process.OutputDataReceived += ConsoleOutputHandler;
        }

        /// <summary>
        /// Parses and redirects mongod logs to ILogger.
        /// </summary>
        /// <param name="process"></param>
        /// <param name="logger"></param>
        private static void WireLogsToLogger(Process process, ILogger logger)
        {
            // Parse the structured log and wire it to logger
            void OnReceivingLogFromMongod(object sender, DataReceivedEventArgs args)
            {
                if (string.IsNullOrWhiteSpace(args.Data))
                    return;
                try
                {
                    var log = JsonSerializer.Deserialize<MongoLogStatement>(args.Data);
                    logger.Log(log.Level,
                        "{message} - {attributes} - {date} - {component} - {context} - {id} - {tags}",
                        log.Message, log.Attributes, log.MongoDate.DateTime, log.Component, log.Context, log.Id, log.Tags);
                }
                catch (Exception ex) when (ex is JsonException || ex is NotSupportedException)
                {
                    logger.LogWarning("Failed parsing the mongod logs {log}. It could be that the format has changed. " +
                        "See: https://docs.mongodb.com/manual/reference/log-messages/#std-label-log-message-json-output-format",
                        args.Data);
                }
                catch (Exception)
                {
                    // Nothing else to do. Swallow the exception and do not wire the logs.
                }
            };
            process.ErrorDataReceived += OnReceivingLogFromMongod;
            process.OutputDataReceived += OnReceivingLogFromMongod;
        }

    }
}
