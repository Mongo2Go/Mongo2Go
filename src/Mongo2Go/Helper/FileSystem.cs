using System.Diagnostics;
using System.IO;

namespace Mongo2Go.Helper
{
    public class FileSystem : IFileSystem
    {
        public void CreateFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        public void DeleteFolder(string path)
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        public void DeleteFile(string fullFileName)
        {
            if (File.Exists(fullFileName))
            {
                File.Delete(fullFileName);
            }
        }

        public void MakeFileExecutable (string path)
        {
            //when on linux or osx we must set the executeble flag on mongo binarys
            // disposed so the process/OS handle is released - MakeFileExecutable runs three times per
            // runner start, so leaking the handle contributes to "too many open files" (#147).
            using (var p = Process.Start("chmod", $"+x {path}"))
            {
                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    throw new IOException($"Could not set executable bit for {path}");
                }
            }
        }
    }
}
