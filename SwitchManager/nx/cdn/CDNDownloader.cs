using SwitchManager.nx.collection;
using SwitchManager.nx.img;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.ObjectModel;
using System.Net.Http.Headers;

namespace SwitchManager.nx.cdn
{

    public class CDNDownloader
    {

        // THIS IS ALL CONFIG
        // TO BE GOTTEN FROM A FILE, PROBABLY
        private string environment;
        private string firmware;
        private string deviceId;
        private static readonly string region = "US";
        private static readonly string titleKeysUrl = "http://snip.li/newkeydb";

        private string imagesPath;
        private static readonly string hactoolPath = "hactool";
        private static readonly string keysPath = "keys.txt";
        private string clientCertPath;
        private static readonly string ShopNPath = "ShopN.pem";
    
        public X509Certificate clientCert { get; }

        // {0} = environment, default lp1 (live production 1?)
        // {1} = titleid - self-evident
        // {2} = title_version - for images, the latest version
        // {3} = device_id, default is just 0s
        private static readonly string remotePathPattern = "https://atum.hac.{0}.d4c.nintendo.net/t/a/{1}/{2}?device_id={3}";

        public List<Task> DownloadTasks { get; } = new List<Task>();

        public CDNDownloader(string clientCertPath, string deviceId, string firmware, string environment, string imagesPath)
        {
            this.clientCertPath = clientCertPath;
            this.clientCert = LoadSSL(clientCertPath);
            this.deviceId = deviceId;
            this.firmware = firmware;
            this.environment = environment;
            this.imagesPath = imagesPath;
            
        }

        private X509Certificate LoadSSL(string path)
        {
            //string contents = File.ReadAllText(path); 
            //byte[] bytes = GetBytesFromPEM(contents, "CERTIFICATE");
            //byte[] bytes = GetBytesFromPEM(contents, "RSA PRIVATE KEY");
            //var certificate = new X509Certificate2(bytes);
            var certificate = new X509Certificate2(path, "");
            //var certificate = X509Certificate.CreateFromSignedFile(path);
            //var certificate = X509Certificate.CreateFromCertFile(path);
            return certificate;
        }

        byte[] GetBytesFromPEM(string pemString, string section)
        {
            var header = String.Format("-----BEGIN {0}-----", section);
            var footer = String.Format("-----END {0}-----", section);

            var start = pemString.IndexOf(header, StringComparison.Ordinal);
            if (start < 0)
                return null;

            start += header.Length;
            var end = pemString.IndexOf(footer, start, StringComparison.Ordinal) - start;

            if (end < 0)
                return null;

            return Convert.FromBase64String(pemString.Substring(start, end));
        }

        public SwitchImage GetLocalImage(string titleID)
        {
            if (Directory.Exists(this.imagesPath))
            {
                string location = this.imagesPath + Path.DirectorySeparatorChar + titleID + ".jpg";
                if (File.Exists(location))
                {
                    SwitchImage img = new SwitchImage(location);
                    return img;
                }
                else
                {
                    return new SwitchImage("Images\\blank.jpg");
                }
            }
            else
            {
                Directory.CreateDirectory(this.imagesPath);
            }

            return null;
        }

        /// <summary>
        /// Loads a remote image from nintendo.
        /// 
        /// This is way more complicated and I know I'm gonna need more arguments passed in.
        /// Not implemented for now.
        /// </summary>
        /// <param name="titleID"></param>
        /// <returns></returns>
        public async Task<SwitchImage> GetRemoteImage(SwitchTitle title)
        {
            // Sanity check if no versions or null then base version of 0
            uint latestVersion;
            if ((title?.Versions?.Count ?? 0) == 0)
                latestVersion = 0;
            else
                latestVersion = title.Versions.First();
            // string deviceID

            string url = string.Format(remotePathPattern, environment, title.TitleID, latestVersion, deviceId);

            var head = HeadRequest(url, null, null);

            string cnmtid = GetHeader(head, "X-Nintendo-Content-ID");
            if (cnmtid != null)
            {
                // Temporary download folder within the images folder for this title
                // Make sure directory is created first
                string gamePath = this.imagesPath + Path.DirectorySeparatorChar + title.TitleID;
                Directory.CreateDirectory(gamePath);

                // CNMT file location in the temp folder
                string fpath = gamePath + Path.DirectorySeparatorChar + cnmtid + ".cnmt.nca";

                // Download file. Function is async and returns a task, and you can wait for it or keep the task around
                // while it finishes
                // Here I'm just waiting for it
                Task t = DownloadFile(url, fpath);
                //this.DownloadTasks.Add(t); // I would totally add this if I knew how to remove it later when it is complete
                await t;

                // Decrypt the CNMT NCA file (all NCA files are encrypted by nintendo)
                var cnmtDir = DecryptNCA(fpath);

                DirectoryInfo sectionDir = cnmtDir.EnumerateDirectories("section0").First();
                DirectoryInfo firstDir = sectionDir.EnumerateDirectories().First();
                FileInfo headerFile = sectionDir.EnumerateFiles("Header.bin").First();

                var cnmt = new CNMT(sectionDir.FullName, headerFile.FullName);

                // Finished downloading file to disk, so now just return the local file
                return GetLocalImage(title.TitleID);
            }
            else
            {
                throw new Exception("No cnmtid found for title " + title.Name);
            }
        }

        private DirectoryInfo DecryptNCA(string fpath)
        {
            return new DirectoryInfo(fpath);
        }

        /// <summary>
        /// Downloads a file from Nintendo and saves it to the specified path.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="fpath"></param>
        private async Task DownloadFile(string url, string fpath)
        {
            var finfo = new FileInfo(fpath);
            long downloaded = 0;
            long fileSize = 0;
            FileStream fs;
            HttpResponseMessage result;

            if (finfo.Exists)
            {
                downloaded = finfo.Length;
                
                result = MakeRequest(HttpMethod.Get, url, null, new Dictionary<string, string>() { { "Range", "bytes=" + downloaded + "-" } });
                var headers = result.Headers;

                if (!"openresty/1.9.7.4".Equals(GetHeader(headers, "Server"))) // Completed download
                {
                    Console.WriteLine("Download complete, skipping: " + fpath);
                    return;
                }
                else if (null == GetHeader(headers, "Content-Range")) // CDN doesn't return a range if request >= filesize
                {
                    string cLength = GetHeader(headers, "Content-Length");
                    if (long.TryParse(cLength, out long lcLength))
                        fileSize = lcLength;
                }
                else
                {
                    string cLength = GetHeader(headers, "Content-Length");
                    if (long.TryParse(cLength, out long lcLength))
                        fileSize = downloaded + lcLength;
                }

                if (downloaded == fileSize)
                {
                    Console.WriteLine("Download complete, skipping: " + fpath);
                    return;
                }
                else if (downloaded < fileSize)
                {
                    Console.WriteLine("Resuming previous download: " + fpath);
                    fs = File.OpenWrite(fpath);
                }
                else
                {
                    Console.WriteLine("Existing file is larger than it should be, restarting: " + fpath);
                    downloaded = 0;
                    fs = File.Create(fpath);
                }
            }
            else
            {
                downloaded = 0;

                result = MakeRequest(HttpMethod.Get, url, null, new Dictionary<string, string>() { { "Range", "bytes=" + downloaded + "-" } });
                var headers = result.Headers;
                string cLength = GetHeader(headers, "Content-Length");
                if (long.TryParse(cLength, out long lcLength))
                    fileSize = lcLength;

                fs = File.Create(fpath);
            }

            // TODO this is where I download the file
            // I can either not have any progress indicators and just DO IT
            // Or I can  create some asynchronous class that maintains a download in a separate thread that communicates with a UI element

            // For now I'm going to do a basic download
            // ...
            // On second though, a bit of research brings up this await and async crap
            // It is confusing because you don't return stuff, you "await" an async task and that implicitly returns a Task
            // but otherwise you always just return with no argument
            // The thing that calls DownloadFile either uses "await" to wait for it to finish or it can 
            // collect tasks somewhere until they're done
            await result.Content.CopyToAsync(fs);
            if (fileSize != 0 && finfo.Length != fileSize)
            {
                throw new Exception("Downloaded file doesn't match expected size after download completion: " + fpath);
            }
            fs.Close();
            Console.WriteLine("Saved file to " + fpath);

            // The next thing to figure out is how to get updates on the task
            // Like, after a task is done I want to remove it from the downloads list
            // and while it is downloading I want to update progress.
            // For downloading an image I will probably just await, but bigger files should have some kind of callback
            // for handling progress and task end. Time for some more research!
        }

        /// <summary>
        /// Queries the CDN for all versions of a game
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public ObservableCollection<uint> GetVersions(SwitchTitle game)
        {
            //string url = string.Format("https://tagaya.hac.{0}.eshop.nintendo.net/tagaya/hac_versionlist", env);
            string url = string.Format("https://superfly.hac.{0}.d4c.nintendo.net/v1/t/{1}/dv", environment, game.TitleID);
            string r = GetRequest(url);

            JObject json = JObject.Parse(r);
            uint latestVersion = json?.Value<uint>("version") ?? 0;

            return GetAllVersions(latestVersion); ;
        }

        /// <summary>
        /// Converts a single version number into a list of all available versions.
        /// </summary>
        /// <param name="versionNo"></param>
        /// <returns></returns>
        public ObservableCollection<uint> GetAllVersions(uint versionNo)
        {
            var versions = new ObservableCollection<uint>();
            for (uint v = versionNo; v > 0; v -= 0x10000)
            {
                versions.Add(v);
            }

            versions.Add(0);
            return versions;
        }

        /// <summary>
        /// Gets ALL games' versions and required versions, whatever that means.
        /// format is {"format_version":1,"last_modified":1533248100}, "titles":[{"id":"01007ef00011e800","version":720896,"required_version":720896},...]}
        /// 
        /// Versions are 0, 0x10000, 0x20000, etc up to the listed number.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string,uint> GetLatestVersions()
        {
            string url = string.Format("https://tagaya.hac.{0}.eshop.nintendo.net/tagaya/hac_versionlist", environment);
            string r = GetRequest(url);

            JObject json = JObject.Parse(r);
            IList<JToken> titles = json["titles"].Children().ToList();

            var result = new Dictionary<string, uint>();
            foreach (var title in titles)
            {
                // Okay so I don't know why (perhaps this has something to do with word alignment? That's too deep in the weeds for me)
                // but every title id ends with 000, except in the results from here they all end with 800
                // Until I understand how it works I'm just going to swap the 8 for a 0.
                StringBuilder tid = new StringBuilder(title.Value<string>("id"));
                tid[13] = '0';
                uint latestVersion = title.Value<uint>("version");
                result[tid.ToString()] = latestVersion;
            }
            return result;
        }

        private string GetHeader(HttpResponseHeaders headers, string name)
        {
            if (headers.Contains(name))
            {
                IEnumerable<string> h = headers.GetValues(name);
                string result = h.First();
                return result;
            }

            return null;
        }

        /// <summary>
        /// Makes a request to Ninty's server, but we only care about getting back the content as a string.
        /// Always a GET request.
        /// /// </summary>
        /// <param name="url"></param>
        /// <param name="cert"></param>
        /// <param name="args"></param>
        private string GetRequest(string url, X509Certificate cert, Dictionary<string, string> args)
        {
            var response = MakeRequest(HttpMethod.Get, url, cert, args);
            string result = response.Content.ReadAsStringAsync().Result;

            return result;
        }

        /// <summary>
        /// Makes a simple reqeust to Ninty's server, using the default client cert and no special headers.
        /// Always a GET request and returns content body as a string.
        /// /// </summary>
        /// <param name="url"></param>
        /// <param name="cert"></param>
        /// <param name="args"></param>
        private string GetRequest(string url)
        {
            var response = MakeRequest(HttpMethod.Get, url, null, null);
            string result = response.Content.ReadAsStringAsync().Result;

            return result;
        }

        /// <summary>
        /// Makes a request to Ninty's server. Gets back only the response headers.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="cert"></param>
        /// <param name="args"></param>
        private HttpResponseHeaders HeadRequest(string url, X509Certificate cert, Dictionary<string, string> args)
        {
            var response = MakeRequest(HttpMethod.Head, url, cert, args);
            return response.Headers;
        }

        /// <summary>
        /// Makes a request to Ninty's server. Gets back the entire response.
        /// WHY THE FUCK IS THIS SO COMPLICATED???? JUST LET ME SEND A REQUEST
        /// </summary>
        /// <param name="method"></param>
        /// <param name="url"></param>
        /// <param name="cert"></param>
        /// <param name="args"></param>
        private HttpResponseMessage MakeRequest(HttpMethod method, string url, X509Certificate cert, Dictionary<string, string> args)
        {
            if (cert == null)
                cert = clientCert;

            string userAgent = string.Format($"NintendoSDK Firmware/{firmware} (platform:NX; eid:{environment})");

            // Create request with method & url, then add headers
            var request = new HttpRequestMessage(method, url);
            request.Headers.Add("User-Agent", userAgent);
            request.Headers.Add("Accept-Encoding", "gzip, deflate");
            request.Headers.Add("Accept", "*/*");
            request.Headers.Add("Connection", "keep-alive");
            
            // Add any additional parameters passed into the method
            if (args != null) args.ToList().ForEach(x => request.Headers.Add(x.Key, x.Value));

            // Add the client certificate
            var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                //SslProtocols = SslProtocols.Tls12,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            handler.ClientCertificates.Add(cert);
            ServicePointManager.ServerCertificateValidationCallback += (o, c, ch, er) => true;

            //            handler.ClientCertificates.Add(new X509Certificate("nx_tls_client_cert.pem"));

            // Create client and get response
            using (var client = new HttpClient(handler))
            {
                var response = client.SendAsync(request).GetAwaiter().GetResult();
                return response;
            }
            // TODO Make http requests async
        }

    }
}