using System.Diagnostics;

namespace Mongo2Go.Helper
{
    public static class MongoImportExport
    {
        public static void Import(string binariesDirectory, int port, string database, string collection, string inputFile, bool drop)
        {
            string fileName = @"{0}\{1}".Formatted(binariesDirectory, MongoDbDefaults.MongoImportExecutable);
            string arguments = @"--host localhost --port {0} --db {1} --collection {2} --file ""{3}""".Formatted(port, database, collection, inputFile);

            if (drop)
            {
                arguments += " --drop";
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
            };

            Process process = new Process { StartInfo = startInfo };
            process.Start();
        }

        public static void Export(string binariesDirectory, int port, string database, string collection, string outputFile)
        {
            string fileName = @"{0}\{1}".Formatted(binariesDirectory, MongoDbDefaults.MongoExportExecutable);
            string arguments = @"--host localhost --port {0} --db {1} --collection {2} --out ""{3}""".Formatted(port, database, collection, outputFile);

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
            };

            Process process = new Process { StartInfo = startInfo };
            process.Start();
        }
    }
}
