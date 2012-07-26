using System.Diagnostics;
using System.Linq;

namespace Mongo2Go.Helper
{
    public class ProcessWatcher : IProcessWatcher
    {
        public bool IsProcessRunning(string processName)
        {
            return Process.GetProcessesByName(processName).Any();
        }
    }
}
