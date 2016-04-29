using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PixartMouseDriver
{
    class NetworkOperations
    {
        private static IPEndPoint QueryRoutingInterface(
          Socket socket,
          IPEndPoint remoteEndPoint)
        {
            SocketAddress address = remoteEndPoint.Serialize();

            byte[] remoteAddrBytes = new byte[address.Size];
            for (int i = 0; i < address.Size; i++)
            {
                remoteAddrBytes[i] = address[i];
            }

            byte[] outBytes = new byte[remoteAddrBytes.Length];
            socket.IOControl(
                        IOControlCode.RoutingInterfaceQuery,
                        remoteAddrBytes,
                        outBytes);
            for (int i = 0; i < address.Size; i++)
            {
                address[i] = outBytes[i];
            }

            EndPoint ep = remoteEndPoint.Create(address);
            return (IPEndPoint)ep;
        }

        private static string testIp(IPAddress ip)
        {
            IPEndPoint remoteEndPoint = new IPEndPoint(ip, 0);
            Socket socket = new Socket(
              AddressFamily.InterNetwork,
              SocketType.Dgram,
              ProtocolType.Udp);
            IPEndPoint localEndPoint = QueryRoutingInterface(socket, remoteEndPoint);
            return localEndPoint.Address.ToString();
        }

        public static string GetIPToHost(string hostname)
        {
            try
            {
                return testIp(IPAddress.Parse(hostname));
            }
            catch { }
            try
            {
                IPHostEntry host = Dns.GetHostEntry(hostname);
                foreach (IPAddress ip in host.AddressList)
                {
                    // Use IPv4 address only
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return testIp(ip);
                    }
                }
            }
            catch { }
            return null;
        }

        public static void sendRequestToEndpoint(String endpoint, String postData)
        {
            WebRequest request = WebRequest.Create(endpoint);
            byte[] data = Encoding.ASCII.GetBytes(postData);
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.ContentLength = data.Length;

            try
            {
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(data, 0, data.Length);
                }
                WebResponse response = request.GetResponse();
            }
            catch { } // TODO: The server terminates the request very suddenly. Try handling this.
        }

        public static string getHostListeningOnPort(int port)
        {
            IPHostEntry host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (IPAddress ip in host.AddressList)
            {
                // Use IPv4 address only
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    Debug.Print("Trying address {0}", ip.ToString());
                    try
                    {
                        using (UdpClient client = new UdpClient(new IPEndPoint(ip, port)))
                        {
                            client.Client.SendTimeout = 300;
                            client.Client.ReceiveTimeout = 300;
                            IPEndPoint broadcast = new IPEndPoint(IPAddress.Broadcast, port);

                            byte[] bytes = System.Text.Encoding.ASCII.GetBytes("syn");
                            client.Send(bytes, bytes.Length, broadcast);

                            while (true)
                            {
                                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                                byte[] response = client.Receive(ref remoteEndPoint);
                                String endpoint = remoteEndPoint.Address.ToString();
                                // Ignore self-responses
                                if (endpoint.Equals(ip.ToString()))
                                {
                                    continue;
                                }
                                return endpoint;
                            }
                        }
                    }
                    catch { }
                }
            }
            return null;
        }
    }
}
