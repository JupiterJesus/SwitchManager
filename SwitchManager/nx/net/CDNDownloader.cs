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

namespace SwitchManager.nx.net
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

        private static readonly string localPath = "Images";
        private static readonly string hactoolPath = "hactool";
        private static readonly string keysPath = "keys.txt";
        private string clientCertPath;
        private static readonly string ShopNPath = "ShopN.pem";
    
        public X509Certificate clientCert { get; }

        // {0} = n
        // {1} = environment
        // {2} = titleid - self-evident
        // {3} = title_version - for images, "base_version" is the last version
        // {4} = device_id
        private static readonly string remotePathPattern = "https://atum{0}.hac.{1}.d4c.nintendo.net/t/a/{2}/{3}?device_id={4}";

        // {0} = tid
        private static readonly string localPathPattern = localPath + Path.DirectorySeparatorChar + "{0}.jpg";

        public CDNDownloader(string clientCertPath, string deviceId, string firmware, string environment)
        {
            this.clientCertPath = clientCertPath;
            this.clientCert = LoadSSL(clientCertPath);
            this.deviceId = deviceId;
            this.firmware = firmware;
            this.environment = environment;
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
        /// <summary>
        /// Loads a remote image from nintendo.
        /// 
        /// This is way more complicated and I know I'm gonna need more arguments passed in.
        /// Not implemented for now.
        /// </summary>
        /// <param name="titleID"></param>
        /// <returns></returns>
        public SwitchImage GetRemoteImage(SwitchTitle game)
        {
            string n = "0";
            string env = "production";
            string titleID = game.TitleID;

            // Sanity check if no versions or null then base version of 0
            uint latestVersion;
            if ((game?.Versions?.Count ?? 0) == 0)
                latestVersion = 0;
            else
                latestVersion = game.Versions.First();
            // string deviceID

            string url = string.Format(remotePathPattern, n, env, titleID, latestVersion, deviceId);

            string r = MakeRequest(HttpMethod.Head, url, null, null);

            return new SwitchImage("Images\\blank.jpg");
        }

        public SwitchImage GetLocalImage(string titleID)
        {
            if (Directory.Exists(localPath))
            {
                string location = string.Format(localPathPattern, titleID);
                if (File.Exists(location))
                {
                    SwitchImage img = new SwitchImage(location);
                    return img;
                }
            }
            else
            {
                Directory.CreateDirectory(localPath);
            }

            return null;
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
            string r = MakeRequest(HttpMethod.Get, url, null, null);

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
            string r = MakeRequest(HttpMethod.Get, url, null, null);

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

        /// <summary>
        /// Makes a request to Ninty's server.
        /// WHY THE FUCK IS THIS SO COMPLICATED???? JUST LET ME SEND A REQUEST
        /// Totally not implemented.
        /// </summary>
        /// <param name="method"></param>
        /// <param name="url"></param>
        private string MakeRequest(HttpMethod method, string url, X509Certificate cert, Dictionary<string,string> args)
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
                string result = response.Content.ReadAsStringAsync().Result;

                return result;
            }
            // TODO Make http requests async
        }

    }
}