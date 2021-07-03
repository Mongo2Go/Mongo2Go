using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using ICSharpCode.SharpZipLib.Zip;

namespace MongoDownloader
{
    internal class ArchiveExtractor
    {
        private readonly HashSet<string> _requiredBinaries;

        public ArchiveExtractor(params string[] requiredBinaries)
        {
            _requiredBinaries = new HashSet<string>(requiredBinaries);
            _requiredBinaries.UnionWith(requiredBinaries.Select(e => Path.ChangeExtension(e, "exe")));
        }

        public void ExtractArchive(Download download, FileInfo archive, DirectoryInfo extractDirectory, CancellationToken cancellationToken)
        {
            switch (Path.GetExtension(archive.Name))
            {
                case ".zip":
                    ExtractZipArchive(download, archive, extractDirectory, cancellationToken);
                    break;
                case ".tgz":
                    ExtractTarGzipArchive(download, archive, extractDirectory, cancellationToken);
                    break;
                default:
                    throw new NotSupportedException($"Only .zip and .tgz archives are currently supported. \"{archive.FullName}\" can not be extracted.");
            }
        }

        private void ExtractZipArchive(Download download, FileInfo archive, DirectoryInfo extractDirectory, CancellationToken cancellationToken)
        {
            // See https://github.com/icsharpcode/SharpZipLib/wiki/FastZip#-how-to-extract-a-zip-file-using-fastzip
            var extractedFileNames = new List<string>();
            var events = new FastZipEvents
            {
                ProcessFile = (_, args) =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    extractedFileNames.Add(args.Name);
                },
            };
            var fastZip = new FastZip(events);
            // Do not extract pdb files at all it saves considerable time, see https://github.com/icsharpcode/SharpZipLib/wiki/FastZip#-using-the-filefilter-parameter for the filter specification
            fastZip.ExtractZip(archive.FullName, extractDirectory.FullName, fileFilter: @"-.*\.pdb");
            FixNonCompliantZip(extractDirectory, extractedFileNames);
            CleanupExtractedFiles(download, extractDirectory, extractedFileNames);
        }

        // The database tools Windows zip has \ instead of / in zip entries
        private static void FixNonCompliantZip(DirectoryInfo extractDirectory, List<string> extractedFileNames)
        {
            if (Path.DirectorySeparatorChar != '\\')
            {
                foreach (var extractedFileName in extractedFileNames.Where(e => e.Contains(@"\")))
                {
                    var extractedPath = Path.Combine(extractDirectory.FullName, extractedFileName);
                    var unixPathInfo = new FileInfo(Path.Combine(extractDirectory.FullName, extractedFileName.Replace('\\', Path.DirectorySeparatorChar)));
                    unixPathInfo.Directory?.Create();
                    File.Move(extractedPath, unixPathInfo.FullName);
                }
            }
        }

        private void ExtractTarGzipArchive(Download download, FileInfo archive, DirectoryInfo extractDirectory, CancellationToken cancellationToken)
        {
            // See https://github.com/icsharpcode/SharpZipLib/wiki/GZip-and-Tar-Samples#-simple-full-extract-from-a-tgz-targz
            using var archiveStream = archive.OpenRead();
            using var gzipStream = new GZipInputStream(archiveStream);
            using var tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.UTF8);
            var extractedFileNames = new List<string>();
            tarArchive.ProgressMessageEvent += (_, entry, _) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                extractedFileNames.Add(entry.Name);
            };
            tarArchive.ExtractContents(extractDirectory.FullName);
            CleanupExtractedFiles(download, extractDirectory, extractedFileNames);
        }

        private void CleanupExtractedFiles(Download download, DirectoryInfo extractDirectory, IEnumerable<string> extractedFileNames)
        {
            var rootDirectoryToDelete = new HashSet<string>();
            foreach (var extractedFileName in extractedFileNames.Select(e => e.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)))
            {
                var extractedFile = new FileInfo(Path.Combine(extractDirectory.FullName, extractedFileName));
                var parts = extractedFileName.Split(Path.DirectorySeparatorChar);
                rootDirectoryToDelete.Add(parts[0]);
                var isBinary = parts.Length > 1 && parts[^2] == "bin";
                if (isBinary && !_requiredBinaries.Contains(parts.Last()))
                {
                    extractedFile.Delete();
                }
                else
                {
                    var destinationPathParts = parts.Skip(1);
                    if (!isBinary)
                    {
                        destinationPathParts = destinationPathParts.Prepend(ProductDirectoryName(download.Product));
                    }
                    var destinationPath = new FileInfo(Path.Combine(destinationPathParts.Prepend(extractDirectory.FullName).ToArray()));
                    destinationPath.Directory?.Create();
                    extractedFile.MoveTo(destinationPath.FullName);
                }
            }
            var rootArchiveDirectory = new DirectoryInfo(Path.Combine(extractDirectory.FullName, rootDirectoryToDelete.Single()));
            var binDirectory = new DirectoryInfo(Path.Combine(rootArchiveDirectory.FullName, "bin"));
            binDirectory.Delete(recursive: false);
            rootArchiveDirectory.Delete(recursive: false);
        }

        private static string ProductDirectoryName(Product product)
        {
            return product switch
            {
                Product.CommunityServer => "community-server",
                Product.DatabaseTools => "database-tools",
                _ => throw new ArgumentOutOfRangeException(nameof(product), product, null)
            };
        }
    }
}