using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;

namespace MongoDownloader
{
    internal static class Program
    {
        private static async Task<int> Main()
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
                var archiveExtractor = new ArchiveExtractor(options);
                var downloader = new MongoDbDownloader(archiveExtractor, options);
                await downloader.RunAsync(toolsDirectory, cancellationTokenSource.Token);
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
    }
}
