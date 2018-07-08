namespace Mongo2Go.Helper
{
    public class MongoDbProcessStarter : IMongoDbProcessStarter
    {
        private const string ProcessReadyIdentifier = "waiting for connections";

        /// <summary>
        /// Starts a new process. Process can be killed
        /// </summary>
        public IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port)
        {
            return Start(binariesDirectory, dataDirectory, port, false);
        }

        /// <summary>
        /// Starts a new process.
        /// </summary>
        public IMongoDbProcess Start(string binariesDirectory, string dataDirectory, int port, bool doNotKill)
        {
            string fileName = @"{0}{1}{2}".Formatted(binariesDirectory, System.IO.Path.DirectorySeparatorChar.ToString(), MongoDbDefaults.MongodExecutable);
            string arguments = @"--nohttpinterface --sslMode disabled --dbpath ""{0}"" --port {1} --nojournal --bind_ip 127.0.0.1".Formatted(dataDirectory, port);

            WrappedProcess wrappedProcess = ProcessControl.ProcessFactory(fileName, arguments);
            wrappedProcess.DoNotKill = doNotKill;

            string windowTitle = "mongod | port: {0}".Formatted(port);
            
            ProcessOutput output = ProcessControl.StartAndWaitForReady(wrappedProcess, 5, ProcessReadyIdentifier, windowTitle);

            MongoDbProcess mongoDbProcess = new MongoDbProcess(wrappedProcess)
                {
                    ErrorOutput = output.ErrorOutput, 
                    StandardOutput = output.StandardOutput
                };

            return mongoDbProcess;
        }
    }
}
