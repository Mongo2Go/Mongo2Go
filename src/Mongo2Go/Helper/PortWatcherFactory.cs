using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Mongo2Go.Helper
{
    public class PortWatcherFactory 
    {
        public static IPortWatcher CreatePortWatcher() 
        {
            IPortWatcher portwatcher = null;

            switch (Environment.OSVersion.Platform) 
            {
                case PlatformID.Unix:
                portwatcher = new UnixPortWatcher ();
                break;

                default:
                portwatcher = new PortWatcher ();
                break;
            }

            return portwatcher;
        }
    }   
}