using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
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
