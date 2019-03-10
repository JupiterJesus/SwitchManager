using System;
using System.Net;
using System.Threading;
using System.Linq;
using System.Text;
using System.Net.Sockets;
using System.IO;
using SwitchManager.nx.library;
using Newtonsoft.Json.Linq;
using SwitchManager.io;
using SwitchManager.util;
using SwitchManager.nx.system;
using System.Collections;
using System.Collections.Generic;

namespace SwitchManager.server
{
    public class NutServer : TCPServer
    {
        private SwitchLibrary Library { get; set; }

        //public NutServer(int port) : base(HandleHTTP, $"http://+:{port}/")
        public NutServer(SwitchLibrary library, int port) : base(IPAddress.Any, port)
        {
            this.Library = library;
            this.ResponderMethod = this.HandleTCP;
        }

        private void HandleHead(string path, WebHeaderCollection headers, Stream response)
        {
            HandleGet(path, headers, response);
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
            // split path by '/'
            string[] pathElements = path.Split(pathChars, StringSplitOptions.RemoveEmptyEntries);

            if (pathElements != null && pathElements.Length >= 2 && "api".Equals(pathElements[0]))
            {
                string action = pathElements[1];
                switch (action)
                {
                    case "download":
                        if (pathElements.Length > 2)
                        {
                            string id = pathElements[2];
                            string start = pathElements.Length > 3 ? pathElements[3] : null;
                            string end = pathElements.Length > 4 ? pathElements[4] : null;
                            HandleGetDownload(response, headers, id, start, end);
                        }
                        break;
                    case "files": HandleGetFiles(response, headers); break;
                    case "info": HandleGetInfo(response, headers, pathElements[2]); break;
                    case "install": HandleGetInstall(response, headers, pathElements[2]); break;
                    case "organize": HandleGetOrganize(response, headers); break;
                    case "preload": HandleGetPreload(response, headers, pathElements[2]); break;
                    case "queue": HandleGetQueue(response, headers); break;
                    case "scan": HandleGetScan(response, headers); break;
                    case "search": HandleGetSearch(response, headers); break;
                    case "tinfoilsetinstalledapps": HandlePostTinfoilSetInstalledApps(response, headers, pathElements[2]); break;
                    case "titles": HandleGetTitles(response, headers); break;
                    case "titleupdates": HandleGetTitleUpdates(response, headers); break;
                    case "updatedb": HandleGetUpdateDb(response, headers); break;
                    case "user": HandleGetUser(response, headers); break;
                    default: WriteError(response, "Unknown action: " + action); break;
                }
            }
        }

        private void HandlePostTinfoilSetInstalledApps(Stream response, WebHeaderCollection headers, string serial)
        {
            string path = $"switch/{serial}/installed.json";
            string sContentLength = headers.Get("Content-Length");
            if (int.TryParse(sContentLength, out int contentLength))
            {
                var str = FileUtils.OpenWriteStream(path);
                byte[] contentBytes = new byte[contentLength];
                response.Read(contentBytes, 0, contentLength);
                str.Write(contentBytes, 0, contentLength);
                WriteSuccess(response, "OK");
            }
            else
            {
                WriteError(response, "Missing Content-Length");
            }
        }

        private void HandleGetDownload(Stream response, WebHeaderCollection headers, string id, string sStart, string sEnd)
        {
            var item = Library.GetTitleByID(id);
            //response.attachFile(nsp.titleId + '.nsp')
            string range = headers.Get("Range");
            // do some downloading shit, make sure to handle start and end of file request

            FileInfo file = new FileInfo(item.RomPath);
            if (sStart == null || !long.TryParse(sStart, out long start))
                start = 0;

            if (sEnd == null || !long.TryParse(sEnd, out long end))
                end = item.Size.Value;

            WriteFile(response, file, start, end);
        }

        private void HandleGetFiles(Stream response, WebHeaderCollection headers)
        {
            throw new NotImplementedException();
        }

        private void HandleGetInfo(Stream response, WebHeaderCollection headers, string v)
        {
            throw new NotImplementedException();
        }

        private void HandleGetInstall(Stream response, WebHeaderCollection headers, string v)
        {
            throw new NotImplementedException();
        }

        private void HandleGetOrganize(Stream response, WebHeaderCollection headers)
        {
            throw new NotImplementedException();
        }

        private void HandleGetPreload(Stream response, WebHeaderCollection headers, string v)
        {
            throw new NotImplementedException();
        }

        private void HandleGetQueue(Stream response, WebHeaderCollection headers)
        {
            throw new NotImplementedException();
        }

        private void HandleGetScan(Stream response, WebHeaderCollection headers)
        {
            throw new NotImplementedException();
        }

        private void HandleGetTitleUpdates(Stream response, WebHeaderCollection headers)
        {
            throw new NotImplementedException();
        }

        private void HandleGetTitles(Stream response, WebHeaderCollection headers)
        {
            throw new NotImplementedException();
        }

        private void HandleGetUpdateDb(Stream response, WebHeaderCollection headers)
        {
            throw new NotImplementedException();
        }

        private void HandleGetUser(Stream response, WebHeaderCollection headers)
        {
            throw new NotImplementedException();
        }

        private void HandleGetSearch(Stream response, WebHeaderCollection headers)
        {
            JArray result = new JArray();
            string queryRegion = "us";
            string queryDLC = null;
            string queryUpdate = null;
            string queryDemo = null;
            string queryPublisher = null;
            
            IEnumerable<SwitchCollectionItem> list = this.Library.Collection; 
            if (queryRegion != null) list = list.Where(item => item.Region != null && item.Region.ToLower().Contains(queryRegion));
            if (queryDLC != null) list = list.Where(item => item.Title.IsDLC.ToString().ToLower().Equals(queryDLC));
            if (queryDemo != null) list = list.Where(item => item.Title.IsDemo.ToString().ToLower().Equals(queryDemo));
            if (queryPublisher != null) list = list.Where(item => item.Publisher != null && item.Publisher.ToLower().Equals(queryPublisher));

            foreach (var item in list)
            {
                if (!"true".Equals(queryUpdate))
                {
                    var json = GetItemJSON(item);
                    result.Add(json);
                }

                if (!"false".Equals(queryUpdate))
                {
                    foreach (var update in item.Updates)
                    {
                        var updateJson = GetItemJSON(update);
                        result.Add(updateJson);
                    }
                }

            }

            string content = result.ToString(Newtonsoft.Json.Formatting.None);
            //content = "[{\"id\":\"0100d1000b18c000\",\"name\":\"1979 Revolution: Black Friday\",\"region\":\"US\",\"size\":4333906456,\"mtime\":636734154867584307}]";
            WriteResponse(response, content);
        }

        private JObject GetItemJSON(SwitchCollectionItem item)
        {
            string id = item.TitleId;
            string name = item.TitleName;
            string region = item.Region != null && (item.Region.Equals("US/EU") || item.Region.Equals("World")) ? "US" : item.Region;
            long size = item.Size ?? 0;
            long modified = item.Added == null ? 0 : item.Added.Value.Ticks;
            JObject json = new JObject
                {
                    { "id", id },
                    { "name", name },
                    { "region", region },
                    { "size", size },
                    { "mtime", modified }
                };

            return json;
        }

        private static void WriteSuccess(Stream response, string content)
        {
            string result = "{'success': True, 'result': " + content + " }";
            WriteResponse(response, result);
        }

        private static void WriteError(Stream response, string content)
        {
            string result = "{'success': False, 'result': " + content + " }";
            WriteResponse(response, result);
        }

        private static void WriteResponse(Stream response, string content)
        {
            StreamWriter writer = new StreamWriter(response, Encoding.ASCII);
            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine($"Date: {DateTime.Now.ToLongTimeString()}");
            writer.WriteLine("Server: SwitchManager v1.3+");
            writer.WriteLine("Accept-Ranges: bytes");
            writer.WriteLine("Content-Type: text/html");
            writer.WriteLine("Content-Length: " + content.Length);
            writer.WriteLine("Connection: close");
            writer.WriteLine();

            byte[] contentBytes = content.Encode(Encoding.UTF8);
            response.Write(contentBytes, 0, contentBytes.Length);
        }

        private static void WriteFile(Stream response, FileInfo file, long start, long end)
        {
            StreamWriter writer = new StreamWriter(response, Encoding.ASCII);
            writer.WriteLine("HTTP/1.1 200 OK");
            writer.WriteLine($"Date: {DateTime.Now.ToLongTimeString()}");
            writer.WriteLine("Server: SwitchManager v1.3+");
            writer.WriteLine("Accept-Ranges: bytes");
            writer.WriteLine($"Content-Range: bytes {start}-{end-1}/{file.Length}");
            writer.WriteLine("Content-Type: application/octet-stream");
            writer.WriteLine("Content-Length: " + (end-start));
            writer.WriteLine();

            // TODO: Write file
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