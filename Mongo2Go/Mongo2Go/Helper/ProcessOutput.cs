using System.Collections.Generic;

namespace Mongo2Go.Helper
{
    public class ProcessOutput
    {
        public ProcessOutput(IEnumerable<string> standardOutput, IEnumerable<string> errorOutput)
        {
            StandardOutput = standardOutput;
            ErrorOutput = errorOutput;
        }

        public IEnumerable<string> StandardOutput { get; private set; }
        public IEnumerable<string> ErrorOutput { get; private set; }
    }
}