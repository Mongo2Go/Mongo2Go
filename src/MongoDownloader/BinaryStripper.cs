using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using ByteSizeLib;
using CliWrap;

namespace MongoDownloader
{
    public class BinaryStripper
    {
        private const string LlvmStripToolName = "llvm-strip";

        private readonly string _llvmStripPath;

        private BinaryStripper(string llvmStripPath)
        {
            _llvmStripPath = llvmStripPath ?? throw new ArgumentNullException(nameof(llvmStripPath));
        }

        public static async Task<BinaryStripper> CreateAsync(CancellationToken cancellationToken)
        {
            var llvmStripPath = await GetLlvmStripPathAsync(cancellationToken);
            return new BinaryStripper(llvmStripPath);
        }

        public async Task<ByteSize> StripAsync(FileInfo executable, CancellationToken cancellationToken = default)
        {
            var sizeBefore = ByteSize.FromBytes(executable.Length);
            await Cli.Wrap(_llvmStripPath).WithArguments(executable.FullName).ExecuteAsync(cancellationToken);
            executable.Refresh();
            var sizeAfter = ByteSize.FromBytes(executable.Length);
            return sizeBefore - sizeAfter;
        }

        private static async Task<string> GetLlvmStripPathAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Cli.Wrap(LlvmStripToolName).WithArguments("--version").ExecuteAsync(cancellationToken);
                // llvm-strip is on the PATH
                return LlvmStripToolName;
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode == 2)
            {
                // llvm-strip is NOT in the PATH, let's search with homebrew
                var llvmStripToolPath = await TryGetLlvmStripPathWithHomebrew();

                if (llvmStripToolPath != null)
                {
                    return llvmStripToolPath;
                }

                throw new FileNotFoundException($"The \"{LlvmStripToolName}\" tool was not found.");
            }
        }

        private static async Task<string?> TryGetLlvmStripPathWithHomebrew()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return null;
            }

            string? llvmStripToolPath = null;
            try
            {
                await Cli.Wrap("brew")
                    // don't validate exit code, if `brew list llvm` fails it's because the llvm formula is not installed
                    .WithValidation(CommandResultValidation.None)
                    .WithArguments(new[] {"list", "llvm"})
                    .WithStandardOutputPipe(PipeTarget.ToDelegate(line =>
                    {
                        if (llvmStripToolPath == null && line.EndsWith(LlvmStripToolName))
                        {
                            llvmStripToolPath = line;
                        }
                    }))
                    .ExecuteAsync();
            }
            catch (Win32Exception exception) when (exception.NativeErrorCode == 2)
            {
                // brew is not installed
                return null;
            }

            return llvmStripToolPath;
        }
    }
}