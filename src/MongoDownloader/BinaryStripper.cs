using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
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

        /// <summary>
        /// The version of <c>llvm-strip</c> that produced the stripped binaries, e.g. <c>Ubuntu LLVM version 18.1.3</c>.
        /// </summary>
        /// <remarks>
        /// Recorded in the checksum manifest. Stripping is deterministic for a given tool version - llvm-strip 14, 15
        /// and 18 were all observed to produce byte-identical output from the same input, on both x64 and arm64 hosts -
        /// but that is an observation, not a guarantee across future releases. Recording the version is what makes
        /// "download it again and check you get the same bytes" a diagnosable comparison rather than a coin toss: if
        /// two people disagree, the first thing to check is whether they stripped with the same tool.
        /// </remarks>
        public string ToolVersion { get; }

        private BinaryStripper(string llvmStripPath, string toolVersion)
        {
            _llvmStripPath = llvmStripPath ?? throw new ArgumentNullException(nameof(llvmStripPath));
            ToolVersion = toolVersion;
        }

        public static async Task<BinaryStripper> CreateAsync(CancellationToken cancellationToken)
        {
            var llvmStripPath = await GetLlvmStripPathAsync(cancellationToken);
            var toolVersion = await GetToolVersionAsync(llvmStripPath, cancellationToken);
            return new BinaryStripper(llvmStripPath, toolVersion);
        }

        private static async Task<string> GetToolVersionAsync(string llvmStripPath, CancellationToken cancellationToken)
        {
            var output = new StringBuilder();
            try
            {
                await Cli.Wrap(llvmStripPath)
                    .WithArguments("--version")
                    .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
                    .ExecuteAsync(cancellationToken);
            }
            catch (Exception)
            {
                return "unknown";
            }

            // llvm-strip prints two lines: a "compatible with GNU strip" banner and the actual version.
            var versionLine = output.ToString()
                .Split('\n')
                .Select(line => line.Trim())
                .FirstOrDefault(line => line.Contains("version", StringComparison.OrdinalIgnoreCase)
                                        && !line.Contains("GNU strip", StringComparison.OrdinalIgnoreCase));

            return string.IsNullOrWhiteSpace(versionLine) ? "unknown" : versionLine;
        }

        public async Task<ByteSize> StripAsync(FileInfo executable, CancellationToken cancellationToken = default)
        {
            var bytesBefore = executable.Length;

            // Keep a copy of the original so stripping can be undone if it turns out not to help.
            var backup = new FileInfo(executable.FullName + ".orig");
            File.Copy(executable.FullName, backup.FullName, overwrite: true);

            try
            {
                await Cli.Wrap(_llvmStripPath).WithArguments(executable.FullName).ExecuteAsync(cancellationToken);
                executable.Refresh();

                // Some binaries carry no removable symbols - notably Windows mongod.exe, whose debug info lives in a
                // separate PDB. There llvm-strip rewrites the file to the same size while changing its bytes: no benefit,
                // and it needlessly breaks provenance against MongoDB's published binary. When stripping does not make
                // the file smaller, restore the original so the bundled binary stays byte-identical to what MongoDB
                // published and the whole tools/ directory remains reproducible by re-running this tool.
                if (executable.Length >= bytesBefore)
                {
                    File.Copy(backup.FullName, executable.FullName, overwrite: true);
                    executable.Refresh();
                    return new ByteSize(0);
                }

                return ByteSize.FromBytes(bytesBefore - executable.Length);
            }
            finally
            {
                backup.Delete();
            }
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