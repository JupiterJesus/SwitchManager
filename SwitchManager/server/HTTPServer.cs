using System;
using System.Net;
using System.Threading;
using System.Linq;
using System.Text;
using SwitchManager.nx.library;
using System.IO;
using SwitchManager.io;
using Newtonsoft.Json.Linq;
using System.Net.Sockets;
using SwitchManager.util;

namespace SwitchManager.server
{
    public class HTTPServer : TCPServer
    {
        private SwitchLibrary Library { get; set; }
        
        public HTTPServer(SwitchLibrary library, int port) : base(IPAddress.Any, port)
        {
            this.Library = library;
            this.ResponderMethod = this.HandleTCP;
        }

        private string HandleHead(string path, WebHeaderCollection headers, Stream response)
        {
            return null;
        }

        private void HandlePost(string path, WebHeaderCollection headers, Stream response)
        {
            // TODO maybe handle authorization but I don't really care
            string auth = headers.Get("Authorization");
            HandleGet(path, headers, response);
        }

        private static readonly char[] pathChars = new char[] { '/' };

        private void HandleGet(string path, WebHeaderCollection headers, Stream response)
        {
            // TODO: This needs to match apache directory listing, I suspect
            if (path.Equals("/"))
            {
                StringBuilder sb = new StringBuilder();
                sb.Append("<html><head><meta http-equiv=\"Content-Type\" content=\"text/html; charset=utf-8\"></head><body><ul>");

                foreach (var item in this.Library.Collection.GetDownloadedTitles())
                {
                    string file = "/" + Uri.EscapeUriString(Path.GetFileName(item.RomPath));
                    sb.Append($"<li><a href='{file}'>{item.TitleName}</a></li>");
                }

                sb.Append("</ul></body></html>");
                string content = sb.ToString();
                WriteResponse(response, content);
            }
            else if (path.StartsWith("/"))
            {
                string file = path.Remove(0, 1);
            }
        }

        private static void WriteResponse(Stream response, string content)
        {
            StreamWriter writer = new StreamWriter(response, Encoding.ASCII);
            writer.Write("HTTP/1.1 200 OK\r\n");
            writer.Write($"Date: {DateTime.Now.ToLongTimeString()}\r\n");
            writer.Write("Server: SwitchManager v1.3+\r\n");
            writer.Write("Accept-Ranges: bytes\r\n");
            writer.Write("Content-Type: text/html\r\n");
            writer.Write("Content-Length: " + content.Length + "\r\n");
            writer.Write("Connection: close\r\n");
            writer.Write("\r\n");

            byte[] contentBytes = content.Encode(Encoding.UTF8);
            response.Write(contentBytes, 0, contentBytes.Length);
        }

        public bool HandleTCP(TcpClient client)
        {
            using (NetworkStream ns = client.GetStream())
            using (StreamReader sr = new StreamReader(ns))
            {
                string http = ReadHttpLine(sr);

                string[] httpLines = http.Split(' ');
                string method = httpLines[0].ToLower();
                string path = httpLines[1].ToLower();
                string protocol = httpLines[2].ToLower();

                WebHeaderCollection headers = new WebHeaderCollection();

                string line = ReadHttpLine(sr);
                while (!string.IsNullOrWhiteSpace(line))
                {
                    headers.Add(line);
                    line = ReadHttpLine(sr);
                }

                switch (method)
                {
                    case "get": HandleGet(path, headers, ns); break;
                    case "post": HandlePost(path, headers, ns); break;
                    case "head": HandleHead(path, headers, ns); break;
                }

                return true;
            }
        }

        private static string ReadHttpLine(StreamReader sr)
        {
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                int c = sr.Read();
                if (c == '\r')
                {
                    int n = sr.Peek();
                    if (n == '\n') // \r\n done
                    {
                        sr.Read();
                        return sb.ToString();
                    }
                }
                sb.Append((char)c);
            }
        }
    }
}