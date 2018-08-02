using SwitchManager.nx.collection;
using SwitchManager.nx.img;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Windows.Web.Http;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Windows.Web.Http.Filters;
using Windows.Security.Cryptography.Certificates;
using Windows.Storage.Streams;
using Windows.Storage;

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

        // {0} = n
        // {1} = environment
        // {2} = titleid - self-evident
        // {3} = title_version - for images, "base_version" is the last version
        // {4} = device_id
        private static readonly string remotePathPattern = "https://atum{0}.hac.{1}.d4c.nintendo.net/t/a/{2}/{3}?device_id={4}";

        // {0} = tid
        private static readonly string localPathPattern = localPath + Path.DirectorySeparatorChar + "{0}.jpg";

        public X509Certificate clientCert { get; }
    
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
        public SwitchImage GetRemoteImage(SwitchGame game)
        {
            string n = "0";
            string env = "production";
            string titleID = game.TitleID;

            string baseVersion;
            if (game.Versions.Count == 0)
                baseVersion = "0";
            else
                baseVersion = game.Versions.Last();
            // string deviceID

            string url = string.Format(remotePathPattern, n, env, titleID, baseVersion, deviceId);

            MakeRequest(HttpMethod.Head, url, null, null);

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
        public List<string> GetVersions(SwitchGame game)
        {
            //string url = string.Format("https://tagaya.hac.{0}.eshop.nintendo.net/tagaya/hac_versionlist", env);
            string url = string.Format("https://superfly.hac.{0}.d4c.nintendo.net/v1/t/{1}/dv", environment, game.TitleID);
            string result = MakeRequest(HttpMethod.Get, url, null, null);

            Dictionary<string,string> json = JsonConvert.DeserializeObject<Dictionary<string, string>>(result);
            string sLatestVersion = json["version"];
            int latestVersion = int.Parse(sLatestVersion);

            List<string> versions = new List<string>();
            if (latestVersion < 65536)
                versions.Add(sLatestVersion);
            else
                versions.Add(sLatestVersion);

            return versions;
            /*
        lastestVer = j['version']
        if lastestVer < 65536:
            return ['%s' % lastestVer]
        else:
            versionList = ('%s' % "-".join(str(i) for i in range(0x10000, lastestVer + 1, 0x10000))).split('-')
            return versionList
    except Exception as e:
        return ['none']

            */
        }
        
        public string GetVersionsList()
        {
            string url = string.Format("https://tagaya.hac.{0}.eshop.nintendo.net/tagaya/hac_versionlist", environment);
            string result = MakeRequest(HttpMethod.Get, url, null, null);

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
            var request = new HttpRequestMessage(method, new Uri(url));
            request.Headers.Add("User-Agent", userAgent);
            request.Headers.Add("Accept-Encoding", "gzip, deflate");
            request.Headers.Add("Accept", "*/*");
            request.Headers.Add("Connection", "keep-alive");
            
            // Add any additional parameters passed into the method
            if (args != null) args.ToList().ForEach(x => request.Headers.Add(x.Key, x.Value));

            // Create a Base Protocol Filter to add certificate errors I want to ignore...
            StorageFolder InstallationFolder = Windows.ApplicationModel.Package.Current.InstalledLocation;
            StorageFile file = InstallationFolder.GetFileAsync(this.clientCertPath).GetResults();
            IBuffer buffer = Windows.Storage.FileIO.ReadBufferAsync(file).GetResults();
            var filter = new HttpBaseProtocolFilter()
            {
                ClientCertificate = new Certificate(buffer),
                AutomaticDecompression = true,
            };
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Expired);
            filter.IgnorableServerCertificateErrors.Add(ChainValidationResult.Untrusted);

            // Create client and get response
            using (var client = new HttpClient(filter))
            {
                var response = client.SendRequestAsync(request).GetAwaiter().GetResult();
                string result = response.Content.ReadAsStringAsync().GetResults();

                return result;
            }
            // TODO Make http requests async
        }

    }
}