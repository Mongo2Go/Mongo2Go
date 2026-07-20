using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace MongoDownloader
{
    internal static class Program
    {
        private static async Task<int> Main(string[] args)
        {
            try
            {
                var toolsDirectory = GetToolsDirectory();

                // Regenerate the checksum manifest from the binaries already present, without downloading anything.
                // The manifest is normally written as part of a download; this exists so it can be repaired without
                // re-fetching several hundred megabytes.
                if (args.Any(e => e == "--write-manifest"))
                {
                    var (server, tools) = ReadVersionsFromDirectoryNames(toolsDirectory);
                    var file = await BinaryManifestWriter.WriteAsync(toolsDirectory, server, tools, CancellationToken.None);
                    AnsiConsole.WriteLine($"Wrote checksum manifest for the binaries in {toolsDirectory.FullName} to {file.FullName}");
                    return 0;
                }

                foreach (DirectoryInfo dir in toolsDirectory.EnumerateDirectories())
                {
                    dir.Delete(true);
                }

                var cancellationTokenSource = new CancellationTokenSource();
                Console.CancelKeyPress += (_, eventArgs) =>
                {
                    // Try to cancel gracefully the first time, then abort the process the second time Ctrl+C is pressed
                    eventArgs.Cancel = !cancellationTokenSource.IsCancellationRequested;
                    cancellationTokenSource.Cancel();
                };
                var options = new Options();
                var performStrip = args.All(e => e != "--no-strip");
                var binaryStripper = performStrip ? await GetBinaryStripperAsync(cancellationTokenSource.Token) : null;
                var archiveExtractor = new ArchiveExtractor(options, binaryStripper);
                var downloader = new MongoDbDownloader(archiveExtractor, options);
                var strippedSize = await downloader.RunAsync(toolsDirectory, cancellationTokenSource.Token);
                if (performStrip)
                {
                    AnsiConsole.WriteLine($"Saved {strippedSize:#.#} by stripping executables");
                }
                return 0;
            }
            catch (Exception exception)
            {
                if (exception is not OperationCanceledException)
                {
                    AnsiConsole.WriteException(exception, ExceptionFormats.ShortenPaths);
                }
                return 1;
            }
        }

        /// <summary>
        /// Recovers the MongoDB and Database Tools versions from the extracted directory names, which follow the
        /// pattern <c>mongodb-&lt;platform&gt;-&lt;arch&gt;-&lt;server&gt;-database-tools-&lt;tools&gt;</c>.
        /// </summary>
        private static (string ServerVersion, string ToolsVersion) ReadVersionsFromDirectoryNames(DirectoryInfo toolsDirectory)
        {
            foreach (var directory in toolsDirectory.EnumerateDirectories("mongodb-*"))
            {
                var match = Regex.Match(directory.Name, @"-(?<server>\d+\.\d+\.\d+)-database-tools-(?<tools>\d+\.\d+\.\d+)$");
                if (match.Success)
                {
                    return (match.Groups["server"].Value, match.Groups["tools"].Value);
                }
            }

            throw new InvalidOperationException(
                $"Could not determine the MongoDB and Database Tools versions from the directory names in " +
                $"\"{toolsDirectory.FullName}\". Expected a directory such as " +
                $"\"mongodb-linux-x64-8.0.0-database-tools-100.14.0\".");
        }

        private static DirectoryInfo GetToolsDirectory()
        {
            for (var directory = new DirectoryInfo("."); directory != null; directory = directory.Parent)
            {
                var toolsDirectory = directory.GetDirectories("tools", SearchOption.TopDirectoryOnly).SingleOrDefault();
                if (toolsDirectory?.Exists ?? false)
                {
                    return toolsDirectory;
                }
            }
            throw new InvalidOperationException("The tools directory was not found");
        }

        private static async Task<BinaryStripper?> GetBinaryStripperAsync(CancellationToken cancellationToken)
        {
            try
            {
                return await BinaryStripper.CreateAsync(cancellationToken);
            }
            catch (FileNotFoundException exception)
            {
                string installCommand;
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    installCommand = "brew install llvm";
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    installCommand = "scoop install llvm";
                else
                    installCommand = "apt-get install llvm";

                throw new Exception($"{exception.Message} Either install llvm with `{installCommand}` or run MongoDownloader with the --no-strip option to skip binary stripping.", exception);
            }
        }
    }
}
