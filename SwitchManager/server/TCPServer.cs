using System;
using System.Net;
using System.Threading;
using System.Linq;
using System.Text;
using System.Net.Sockets;

namespace SwitchManager.server
{
    public class TCPServer
    {
        private readonly TcpListener listener;
        protected Func<TcpClient, bool> ResponderMethod { get; set; }
        protected bool Listening { get; set; } = false;

        public TCPServer(Func<TcpClient, bool> method, IPAddress addr, int port)
        {
            listener = new TcpListener(addr, port);
            ResponderMethod = method ?? throw new ArgumentException("method");
            listener.Start();
        }

        public TCPServer(IPAddress addr, int port) : this(StubResponder, addr, port)
        {

        }

        public static bool StubResponder(TcpClient client)
        {
            return true;
        }

        public void Run()
        {
            Listening = true;
            ThreadPool.QueueUserWorkItem((o) =>
            {
                Console.WriteLine("Webserver running...");
                try
                {
                    while (Listening)
                    {
                        if (listener.Pending())
                        {
                            TcpClient client = listener.AcceptTcpClient();
                            Console.WriteLine("Connection accepted.");

                            var childSocketThread = new Thread(() =>
                            {
                                ResponderMethod(client);
                                //byte[] data = new byte[100];
                                //int size = client.Receive(data);
                                //Console.WriteLine("Recieved data: ");
                                //for (int i = 0; i < size; i++)

                                client.Close();
                            });
                            childSocketThread.Start();
                        }
                        else
                        {
                            Thread.Sleep(100); //<--- timeout
                        }
                    }
                }
                catch { } // suppress any exceptions
            });
        }

        public void Stop()
        {
            listener.Stop();
            Listening = false;
        }
    }
}