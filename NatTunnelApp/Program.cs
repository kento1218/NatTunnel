using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;

namespace NatTunnel
{
    class Program
    {
        static void Main(string[] args)
        {
            var config = Properties.Settings.Default;
            IPAddress forwardIP = null;
            if (config.ForwardIP.Length > 0)
            {
                forwardIP = IPAddress.Parse(config.ForwardIP);
            }

            var tunnel = new Tunnel()
            {
                StunServer = config.StunServer,
                StunPort = config.StunPort,
                LocalPort = config.LocalPort,
                ForwardPort = config.ForwardPort,
                ForwardIP = forwardIP
            };

            tunnel.Publish();
            Console.WriteLine("Public: {0}:{1}", tunnel.PublicEndPoint.Address, tunnel.PublicEndPoint.Port);

            Console.Write("Remote? ");
            var remoteStr = Console.ReadLine();
            var remote = remoteStr.Split(':');
            tunnel.RemoteIP = IPAddress.Parse(remote[0]);
            tunnel.RemotePort = int.Parse(remote[1]);

            tunnel.Start();
            Console.WriteLine("Press any key to quit.");
            Thread.Sleep(100);

            Thread pingThread = null;
            if(config.LocalPort == 0)
            {
                var pingSock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                var buffer = new byte[] { 0xfe, 0xfe, 0xfe };
                pingThread = new Thread(() =>
                {
                    while (tunnel.DownBytes == 0)
                    {
                        pingSock.SendTo(buffer, new IPEndPoint(IPAddress.Loopback, tunnel.ActualLocalPort));
                        Thread.Sleep(1000);
                    }
                });
                pingThread.Start();
            }

            Console.ReadKey();
            if (pingThread != null) pingThread.Abort();
            tunnel.Stop();
        }
    }
}
