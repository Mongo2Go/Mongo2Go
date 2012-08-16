using System.Diagnostics;
using System.IO;

namespace Mongo2Go.Helper
{
    public static class MongoImportExport
    {
        /// <summary>
        /// Input File: Absolute path stays unchanged, relative path will be relative to current executing directory (usually the /bin folder)
        /// </summary>
        public static ProcessOutput Import(string binariesDirectory, int port, string database, string collection, string inputFile, bool drop)
        {
            string finalPath = FolderSearch.FinalizePath(inputFile);
            
            string fileName = @"{0}\{1}".Formatted(binariesDirectory, MongoDbDefaults.MongoImportExecutable);
            string arguments = @"--host localhost --port {0} --db {1} --collection {2} --file ""{3}""".Formatted(port, database, collection, finalPath);
            if (drop) { arguments += " --drop"; }

            Process process = ProcessControl.ProcessFactory(fileName, arguments);

            string windowTitle = "mongoimport | port: {0} db: {1} collection: {2} file {3}".Formatted(port, database, collection, inputFile);
            return ProcessControl.StartAndWaitForExit(process, windowTitle);
        }

        /// <summary>
        /// Output File: Absolute path stays unchanged, relative path will be relative to current executing directory (usually the /bin folder)
        /// </summary>
        public static ProcessOutput Export(string binariesDirectory, int port, string database, string collection, string outputFile)
        {
            string finalPath = FolderSearch.FinalizePath(outputFile);

            string fileName = @"{0}\{1}".Formatted(binariesDirectory, MongoDbDefaults.MongoExportExecutable);
            string arguments = @"--host localhost --port {0} --db {1} --collection {2} --out ""{3}""".Formatted(port, database, collection, finalPath);

            Process process = ProcessControl.ProcessFactory(fileName, arguments);

            string windowTitle = "mongoexport | port: {0} db: {1} collection: {2} file {3}".Formatted(port, database, collection, outputFile);
            return ProcessControl.StartAndWaitForExit(process, windowTitle);
        }
    }
}
