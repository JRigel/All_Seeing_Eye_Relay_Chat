using System;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace DDE_Server
{
    class IRC_Socket
    {
        private IPEndPoint endPoint;
        private Socket socket;
        private ClientHandler handler;
        public void Run()
        {
            Thread acceptThread1 = new Thread(new ThreadStart(acceptThread));
            acceptThread1.Start();
        }  

        void acceptThread()
        {
            try
            {
                endPoint = new IPEndPoint(IPAddress.Any, 9046); // ip & port
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                socket.Bind(endPoint);
                socket.Listen(5);
                Socket[] clientSockets = new Socket[5];
                //IPHostEntry host = Dns.Resolve(Dns.GetHostName());
                //Console.WriteLine("[DEBUG] " + host.HostName + " " + host.AddressList[0].ToString());
                Console.WriteLine("[DEBUG MODE] [" + Dns.GetHostName() + "] [" + Dns.GetHostAddresses(Dns.GetHostName())[3] + "]");
                int idx = 0;
                while (true)
                {
                    clientSockets[idx] = socket.Accept();
                    //clientSockets[idx].Blocking = false; // 논블로킹
                    clientSockets[idx].NoDelay = true;
                    Console.WriteLine("Client Log-in : " + clientSockets[idx].RemoteEndPoint.ToString());
                    handler = new ClientHandler(clientSockets[idx]);
                }
            }
            catch
            {
                //Environment.Exit(0);
            }
        }
    }
}