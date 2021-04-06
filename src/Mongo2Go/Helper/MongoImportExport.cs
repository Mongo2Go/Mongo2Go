using System.Diagnostics;
using System.IO;

namespace Mongo2Go.Helper
{
    public static class MongoImportExport
    {
        /// <summary>
        /// Input File: Absolute path stays unchanged, relative path will be relative to current executing directory (usually the /bin folder)
        /// </summary>
        public static ProcessOutput Import(string binariesDirectory, int port, string database, string collection, string inputFile, bool drop, string additionalMongodArguments = null)
        {
            string finalPath = FolderSearch.FinalizePath(inputFile);

            if (!File.Exists(finalPath))
            {
                throw new FileNotFoundException("File not found", finalPath);
            }

            string fileName = Path.Combine("{0}", "{1}").Formatted(binariesDirectory, MongoDbDefaults.MongoImportExecutable);
            string arguments = @"--host localhost --port {0} --db {1} --collection {2} --file ""{3}""".Formatted(port, database, collection, finalPath);
            if (drop) { arguments += " --drop"; }
            arguments += MongodArguments.GetValidAdditionalArguments(arguments, additionalMongodArguments);

            Process process = ProcessControl.ProcessFactory(fileName, arguments);

            return ProcessControl.StartAndWaitForExit(process);
        }

        /// <summary>
        /// Output File: Absolute path stays unchanged, relative path will be relative to current executing directory (usually the /bin folder)
        /// </summary>
        public static ProcessOutput Export(string binariesDirectory, int port, string database, string collection, string outputFile, string additionalMongodArguments = null)
        {
            string finalPath = FolderSearch.FinalizePath(outputFile);

            string fileName = Path.Combine("{0}", "{1}").Formatted(binariesDirectory, MongoDbDefaults.MongoExportExecutable);
            string arguments = @"--host localhost --port {0} --db {1} --collection {2} --out ""{3}""".Formatted(port, database, collection, finalPath);
            arguments += MongodArguments.GetValidAdditionalArguments(arguments, additionalMongodArguments);

            Process process = ProcessControl.ProcessFactory(fileName, arguments);

            return ProcessControl.StartAndWaitForExit(process);
        }
    }
}
