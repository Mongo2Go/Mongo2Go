using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace Mongo2Go.Helper
{
    public class PortWatcher : IPortWatcher
    {
        public bool IsPortAvailable(int portNumber)
        {
            IPEndPoint[] tcpConnInfoArray = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            return tcpConnInfoArray.All(endpoint => endpoint.Port != portNumber);
        }
    }
}
