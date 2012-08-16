using System.Diagnostics;

namespace Mongo2Go.Helper
{
    public class WrappedProcess : Process
    {
        public bool DoNotKill { get; set; }
    }
}
