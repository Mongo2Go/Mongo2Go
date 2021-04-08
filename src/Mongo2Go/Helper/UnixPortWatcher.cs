using System.Net;
using System.Net.Sockets;

namespace Mongo2Go.Helper
{

    public class UnixPortWatcher : IPortWatcher
    {
        public int FindOpenPort ()
        {
            // Locate a free port on the local machine by binding a socket to
            // an IPEndPoint using IPAddress.Any and port 0. The socket will
            // select a free port.
            int listeningPort = 0;
            Socket portSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                IPEndPoint socketEndPoint = new IPEndPoint(IPAddress.Any, 0);
                portSocket.Bind(socketEndPoint);
                socketEndPoint = (IPEndPoint)portSocket.LocalEndPoint;
                listeningPort = socketEndPoint.Port;
            }
            finally
            {
                portSocket.Close();
            }

            return listeningPort;
        }

        public bool IsPortAvailable (int portNumber)
        {
            TcpListener tcpListener = new TcpListener (IPAddress.Loopback, portNumber);
            try {
                tcpListener.Start ();
                return true;
            }
            catch (SocketException) {
                return false;
            } finally
            {
                tcpListener.Stop ();
            }
        }
    }
}
