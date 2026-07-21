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

        /// <summary>
        /// Caps how many <c>llvm-strip</c> processes run at once, across all platforms/binaries being processed.
        /// </summary>
        /// <remarks>
        /// llvm-strip loads the whole binary into memory (roughly 2-3x its size - a MongoDB 8.x mongod is ~190 MB),
        /// so this is the dominant memory cost of a download. Without a global cap, a multi-platform run fans out one
        /// strip per binary (e.g. two platforms extracting three binaries each = six concurrent strips) and exhausts
        /// RAM on a modest machine. This limit is separate from - and stricter than - the download/extract concurrency,
        /// because downloads are I/O-bound and cheap in memory while strips are memory-bound.
        /// </remarks>
        private const int MaxConcurrentStrips = 2;

        private static readonly SemaphoreSlim StripThrottle = new SemaphoreSlim(MaxConcurrentStrips);

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
            // Bound concurrent llvm-strip processes (the memory hog) regardless of how many binaries are queued.
            await StripThrottle.WaitAsync(cancellationToken);
            try
            {
                return await StripCoreAsync(executable, cancellationToken);
            }
            finally
            {
                StripThrottle.Release();
            }
        }

        private enum MachOKind
        {
            /// <summary>Not a Mach-O binary (e.g. an ELF or PE executable).</summary>
            NotMachO,
            /// <summary>A Mach-O binary targeting Apple Silicon (arm64), which the kernel refuses to run unsigned.</summary>
            AppleSilicon,
            /// <summary>Any other Mach-O binary (notably Intel x86_64), which runs fine unsigned.</summary>
            Other,
        }

        /// <summary>
        /// Classifies a file by its Mach-O header so the caller can tell an Apple Silicon binary (which must be signed to
        /// run) from an Intel one (which need not be) and from a non-Mach-O file.
        /// </summary>
        private static async Task<MachOKind> ReadMachOKindAsync(FileInfo file, CancellationToken cancellationToken)
        {
            var header = new byte[8];
            await using var stream = file.OpenRead();
            if (await stream.ReadAsync(header, 0, header.Length, cancellationToken) < header.Length)
            {
                return MachOKind.NotMachO;
            }

            var magic = (uint)((header[0] << 24) | (header[1] << 16) | (header[2] << 8) | header[3]);
            var isMachO = magic is 0xFEEDFACE or 0xFEEDFACF // Mach-O 32/64-bit, big-endian
                or 0xCEFAEDFE or 0xCFFAEDFE                 // Mach-O 32/64-bit, little-endian
                or 0xCAFEBABE or 0xBEBAFECA;                // universal ("fat") binary
            if (!isMachO)
            {
                return MachOKind.NotMachO;
            }

            // MongoDB ships thin, little-endian macOS binaries (separate x86_64 and arm64 archives, never a fat binary),
            // so the CPU type is the four little-endian bytes right after the magic. Anything that isn't positively a
            // little-endian thin Mach-O is treated as "Other": strip it, but don't assume it needs an arm64 signature.
            var littleEndian64 = magic is 0xCFFAEDFE;
            if (!littleEndian64)
            {
                return MachOKind.Other;
            }

            var cpuType = (uint)(header[4] | (header[5] << 8) | (header[6] << 16) | (header[7] << 24));
            const uint cpuTypeArm64 = 0x0100000C; // CPU_TYPE_ARM (12) | CPU_ARCH_ABI64 (0x01000000)
            return cpuType == cpuTypeArm64 ? MachOKind.AppleSilicon : MachOKind.Other;
        }

        private async Task<ByteSize> StripCoreAsync(FileInfo executable, CancellationToken cancellationToken)
        {
            var bytesBefore = executable.Length;

            // Keep a copy of the original so stripping can be undone if it turns out not to help.
            var backup = new FileInfo(executable.FullName + ".orig");
            File.Copy(executable.FullName, backup.FullName, overwrite: true);

            try
            {
                // MongoDB ships its macOS binaries code-signed, and macOS treats a signed Mach-O as immutable: llvm-strip
                // fails on it with "Operation not permitted". The signature has to come off before stripping and go back
                // on afterwards - crucially, an unsigned arm64 binary is SIGKILLed on sight by the Apple Silicon kernel
                // (arm64 macOS mandates at least an ad-hoc signature), so a stripped-but-unsigned mongod would not run on
                // any Apple Silicon Mac, ours or a consumer's. Re-signing ad-hoc (--sign -) satisfies that requirement;
                // it is not notarized, which is fine because Mongo2Go executes the binary directly rather than through
                // Gatekeeper quarantine. Only applies to Mach-O binaries on a macOS host; a no-op everywhere else.
                var machOKind = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                    ? await ReadMachOKindAsync(executable, cancellationToken)
                    : MachOKind.NotMachO;
                if (machOKind != MachOKind.NotMachO)
                {
                    await Cli.Wrap("codesign")
                        .WithArguments(new[] { "--remove-signature", executable.FullName })
                        .WithValidation(CommandResultValidation.None)
                        .ExecuteAsync(cancellationToken);
                    executable.Refresh();
                }

                await Cli.Wrap(_llvmStripPath).WithArguments(executable.FullName).ExecuteAsync(cancellationToken);
                executable.Refresh();

                if (machOKind == MachOKind.AppleSilicon)
                {
                    // Ad-hoc re-sign the stripped binary so it will run on Apple Silicon (see above). --force overwrites
                    // any residual signature. This grows the file by the size of the (tiny) signature blob, so it must
                    // happen before the "did stripping actually shrink it?" check below, which compares final bytes.
                    // Only arm64 needs this: the Intel x64 binary runs unsigned (as Mongo2Go's macOS binaries always
                    // have), so it is left stripped-and-unsigned, which also keeps its incompressible signature hashes
                    // out of the size-constrained package.
                    await Cli.Wrap("codesign")
                        .WithArguments(new[] { "--sign", "-", "--force", executable.FullName })
                        .ExecuteAsync(cancellationToken);
                    executable.Refresh();
                }

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
                // Validation disabled on purpose: this probe only needs to know whether llvm-strip is on the PATH,
                // i.e. whether launching it throws "file not found". Its exit code is irrelevant here - and
                // "llvm-strip --version" returns 74 (EX_IOERR) in environments where its stdout pipe is closed
                // early, which would otherwise make CliWrap throw and wrongly report the tool as missing even
                // though the actual strip (which writes nothing to stdout) works fine.
                await Cli.Wrap(LlvmStripToolName).WithArguments("--version").WithValidation(CommandResultValidation.None).ExecuteAsync(cancellationToken);
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