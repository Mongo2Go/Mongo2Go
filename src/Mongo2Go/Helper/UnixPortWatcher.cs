using System.Net;
using System.Net.Sockets;

namespace Mongo2Go.Helper
{

    public class UnixPortWatcher : IPortWatcher
    {
        public int FindOpenPort (int startPort)
        {
            int port = startPort;
            do {
                if (IsPortAvailable (port)) {
                    break;
                }

                if (port == MongoDbDefaults.TestStartPort + 100) {
                    throw new NoFreePortFoundException ();
                }

                ++port;

            } while (true);

            return port;
        }

        public bool IsPortAvailable (int portNumber)
        {
            bool result = false;

            TcpListener tcpListener = new TcpListener (IPAddress.Loopback, portNumber);
            try {                
                tcpListener.Start ();
            } catch (SocketException) {
                result = false;

            } finally 
            {
                tcpListener.Stop ();
                result = true;
            }

            return result;
        }
    }
}
