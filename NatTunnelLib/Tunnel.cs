using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using LumiSoft.Net.STUN.Client;

namespace NatTunnel
{
    public class Tunnel
    {
        private Thread UpThread;
        private Thread DownThread;
        private EventWaitHandle StopEvent;

        private Socket RemoteSocket;
        private Socket LocalSocket;

        public string StunServer { get; set; }
        public int StunPort { get; set; }
        public IPAddress RemoteIP { get; set; }
        public int RemotePort { get; set; }
        public int ForwardPort { get; set; }
        public IPAddress ForwardIP { get; set; }
        public int LocalPort { get; set; }
        public ulong UpBytes { get; private set; }
        public ulong DownBytes { get; private set; }

        public IPEndPoint PublicEndPoint { get; private set; }

        public int ActualLocalPort { get { return ((IPEndPoint)LocalSocket.LocalEndPoint).Port; } }

        public void Publish()
        {
            RemoteSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            RemoteSocket.Bind(new IPEndPoint(IPAddress.Any, 0));

            var result = STUN_Client.Query(StunServer, StunPort, RemoteSocket);
            PublicEndPoint = result.PublicEndPoint;
        }

        public void Start()
        {
            LocalSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            LocalSocket.Bind(new IPEndPoint(IPAddress.Any, LocalPort));

            UpBytes = 0;
            DownBytes = 0;

            StopEvent = new ManualResetEvent(false);
            UpThread = new Thread(() =>
            {
                var buffer = new byte[2048];
                LocalSocket.ReceiveTimeout = 50;

                while (!StopEvent.WaitOne(0))
                {
                    try
                    {
                        var ret = LocalSocket.Receive(buffer);
                        UpBytes += (ulong)ret;
                        if (RemoteIP != null)
                        {
                            var remoteEP = new IPEndPoint(RemoteIP, RemotePort);
                            RemoteSocket.SendTo(buffer, ret, SocketFlags.None, remoteEP);
                        }
                    }
                    catch (SocketException)
                    {
                    }
                }
            });
            DownThread = new Thread(() =>
            {
                var buffer = new byte[2048];
                RemoteSocket.ReceiveTimeout = 50;

                while (!StopEvent.WaitOne(0))
                {
                    try
                    {
                        var ret = RemoteSocket.Receive(buffer);
                        DownBytes += (ulong)ret;
                        if (ForwardIP != null)
                        {
                            var forwardEP = new IPEndPoint(ForwardIP, ForwardPort);
                            LocalSocket.SendTo(buffer, ret, SocketFlags.None, forwardEP);
                        }
                    }
                    catch (SocketException)
                    {
                    }
                }
            });
            UpThread.Start();
            DownThread.Start();
        }

        public void Stop()
        {
            StopEvent.Set();
            UpThread.Join();
            DownThread.Join();
        }
    }
}
