using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace Mongo2Go.Helper
{
    public class PortWatcher : IPortWatcher
    {
        public int FindOpenPort(int startPort)
        {
            int port = startPort;
            do
            {
                if (IsPortAvailable(port))
                {
                    break;
                }

                if (port == MongoDbDefaults.TestStartPort + 100)
                {
                    throw new NoFreePortFoundException();
                }

                ++port;

            } while (true);

            return port;
        }

        public bool IsPortAvailable(int portNumber)
        {
            IPEndPoint[] tcpConnInfoArray = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
            return tcpConnInfoArray.All(endpoint => endpoint.Port != portNumber);
        }
    }
}
