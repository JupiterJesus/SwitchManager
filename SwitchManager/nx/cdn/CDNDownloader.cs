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
using System.Diagnostics;

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
        private string hactoolPath = "hactool";
        private string keysPath = "keys.txt";
        private string clientCertPath;
        private static readonly string ShopNPath = "ShopN.pem";
    
        public X509Certificate clientCert { get; }

        public List<Task> DownloadTasks { get; } = new List<Task>();

        public CDNDownloader(string clientCertPath, string deviceId, string firmware, string environment, string imagesPath, string hactoolPath, string keysPath)
        {
            this.clientCertPath = clientCertPath;
            this.clientCert = LoadSSL(clientCertPath);
            this.deviceId = deviceId;
            this.firmware = firmware;
            this.environment = environment;
            this.imagesPath = Path.GetFullPath(imagesPath);
            this.hactoolPath = Path.GetFullPath(hactoolPath);
            this.keysPath = Path.GetFullPath(keysPath);
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
                    return null;
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
            uint version;
            if ((title?.Versions?.Count ?? 0) == 0)
                version = 0;
            else
                version = title.Versions.Last(); // I don't know if this is supposed to be the newest or oldest version
            // string deviceID

            string url = $"https://atum.hac.{environment}.d4c.nintendo.net/t/a/{title.TitleID}/{version}?device_id={deviceId}";

            var head = await HeadRequest(url, null, null).ConfigureAwait(false);

            string cnmtid = GetHeader(head, "X-Nintendo-Content-ID");
            if (cnmtid != null)
            {
                // Temporary download folder within the images folder for this title
                // Make sure directory is created first
                string gamePath = this.imagesPath + Path.DirectorySeparatorChar + title.TitleID;
                DirectoryInfo gameDir = Directory.CreateDirectory(gamePath);

                // CNMT file location in the temp folder
                string fpath = gamePath + Path.DirectorySeparatorChar + cnmtid + ".cnmt.nca";

                // Download file. Function is async and returns a task, and you can wait for it or keep the task around while it finishes
                // We need it NOW though and can't do anything until it is done, so no awaiting...
                await DownloadFile(url, fpath).ConfigureAwait(false);

                // Decrypt the CNMT NCA file (all NCA files are encrypted by nintendo)
                // Hactool does the job for us
                var cnmtDir = DecryptNCA(fpath);

                // For CNMTs, there is a section0 containing a single cnmt file, plus a Header.bin right next to section0
                var sectionDirInfo = cnmtDir.EnumerateDirectories("section0").First();
                var extractedCnmt = sectionDirInfo.EnumerateFiles().First();
                var headerFile = cnmtDir.EnumerateFiles("Header.bin").First();

                var cnmt = new CNMT(extractedCnmt.FullName, headerFile.FullName);

                // Parse "control" type content entries inside the NCA (just one...)
                // Download each file (just one)

                string ncaID = cnmt.Parse(NCAType.Control).Keys.First(); // There's only one control.nca
                url = $"https://atum.hac.{environment}.d4c.nintendo.net/c/c/{ncaID}?device_id={deviceId}";
                fpath = gamePath + Path.DirectorySeparatorChar + "control.nca";
                await DownloadFile(url, fpath); // download file and wait for it since we can't do anything until it is done

                var controlDir = DecryptNCA(fpath);

                sectionDirInfo = controlDir.EnumerateDirectories("romfs").First();

                var iconFile = sectionDirInfo.EnumerateFiles("icon_*.dat").First(); // Get all icon files in section0, should just be one
                iconFile.MoveTo(imagesPath + Path.DirectorySeparatorChar + title.TitleID + ".jpg");
                gameDir.Delete(true);

                // Finished downloading file to disk, so now just return the local file
                return GetLocalImage(title.TitleID);
            }
            else
            {
                throw new Exception("No cnmtid found for title " + title.Name);
            }
        }

        /// <summary>
        /// 
        /// TODO Implement DecryptNCA
        /// </summary>
        /// <param name="fpath"></param>
        /// <returns></returns>
        private DirectoryInfo DecryptNCA(string ncaPath, string outDir = null)
        { 
            string fName = Path.GetFileNameWithoutExtension(ncaPath); // fName = os.path.basename(fPath).split()[0]
            if (outDir == null)
                outDir = Path.GetDirectoryName(ncaPath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(ncaPath);
            DirectoryInfo outDirInfo = new DirectoryInfo(outDir);
            outDirInfo.Create();

            string hactoolExe = (this.hactoolPath);
            string keysFile = (this.keysPath);
            string exefsPath = (outDir + Path.DirectorySeparatorChar + "exefs");
            string romfsPath = (outDir + Path.DirectorySeparatorChar + "romfs");
            string section0Path = (outDir + Path.DirectorySeparatorChar + "section0");
            string section1Path = (outDir + Path.DirectorySeparatorChar + "section1");
            string section2Path = (outDir + Path.DirectorySeparatorChar + "section2");
            string section3Path = (outDir + Path.DirectorySeparatorChar + "section3");
            string headerPath = (outDir + Path.DirectorySeparatorChar + "Header.bin");

            // NOTE: Using single quotes here instead of single quotes fucks up windows, it CANNOT handle single quotes
            // Anything surrounded in single quotes will throw an error because the file/folder isn't found
            // Must use escaped double quotes!
            string commandLine = $" -k \"{keysFile}\"" +
                                 $" --exefsdir=\"{exefsPath}\"" +
                                 $" --romfsdir=\"{romfsPath}\"" +
                                 $" --section0dir=\"{section0Path}\"" +
                                 $" --section1dir=\"{section1Path}\"" +
                                 $" --section2dir=\"{section2Path}\"" +
                                 $" --section3dir=\"{section3Path}\"" +
                                 $" --header=\"{headerPath}\"" +
                                 $" \"{ncaPath}\"";
            try
            {
                ProcessStartInfo hactoolSI = new ProcessStartInfo()
                {
                    FileName = hactoolExe,
                    WorkingDirectory = System.IO.Directory.GetCurrentDirectory(),
                    Arguments = commandLine,
                    UseShellExecute = false,
                    //RedirectStandardOutput = true,
                    //RedirectStandardError = true,
                    CreateNoWindow = true,
                };
                Process hactool = Process.Start(hactoolSI);

                //string errors = hactool.StandardError.ReadToEnd();
                //string output = hactool.StandardOutput.ReadToEnd();
                
                hactool.WaitForExit();

                if (outDirInfo.GetDirectories().Length == 0)
                    throw new Exception($"Running hactool failed, output directory {outDir} is empty!");
            }
            catch (Exception e)
            {
                throw new Exception("Hactool decryption failed!", e);
            }

            return outDirInfo;
        }
        
        /// <summary>
        /// TODO Implement DownloadTitle
        /// </summary>
        /// <param name="title"></param>
        /// <param name="version"></param>
        /// <param name="nspRepack"></param>
        /// <param name="verify"></param>
        /// <param name="pathDir"></param>
        /// <returns></returns>
        public async Task DownloadTitle(SwitchTitle title, uint version, bool nspRepack = false, bool verify = false, string pathDir = null)
        {
            throw new NotImplementedException();
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
            long expectedSize = 0;
            FileStream fs;
            HttpResponseMessage result;

            if (finfo.Exists)
            {
                downloaded = finfo.Length;
                
                result = await MakeRequest(HttpMethod.Get, url, null, new Dictionary<string, string>() { { "Range", "bytes=" + downloaded + "-" } });
                
                if (!"openresty/1.9.7.4".Equals(GetHeader(result.Headers, "Server"))) // Completed download
                {
                    Console.WriteLine("Download complete, skipping: " + fpath);
                    return;
                }
                else if (null == GetContentHeader(result, "Content-Range")) // CDN doesn't return a range if request >= filesize
                {
                    string cLength = GetContentHeader(result, "Content-Length");
                    if (long.TryParse(cLength, out long lcLength))
                        expectedSize = lcLength;
                }
                else
                {
                    string cLength = GetContentHeader(result, "Content-Length");
                    if (long.TryParse(cLength, out long lcLength))
                        expectedSize = downloaded + lcLength;
                }

                if (downloaded == expectedSize)
                {
                    Console.WriteLine("Download complete, skipping: " + fpath);
                    return;
                }
                else if (downloaded < expectedSize)
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
                fs = File.Create(fpath);
                downloaded = 0;

                result = await MakeRequest(HttpMethod.Get, url);
                string cLength = GetContentHeader(result, "Content-Length");
                if (long.TryParse(cLength, out long lcLength))
                    expectedSize = lcLength;
            }

            // this is where I download the file
            // I can either not have any progress indicators and just DO IT
            // Or I can  create some asynchronous class that maintains a download in a separate thread that communicates with a UI element

            // For now I'm going to do a basic download
            // ...
            // On second though, a bit of research brings up this await and async crap
            // It is confusing because you don't return stuff, you "await" an async task and that implicitly returns a Task
            // but otherwise you always just return with no argument
            // The thing that calls DownloadFile either uses "await" to wait for it to finish or it can 
            // collect tasks somewhere until they're done. Right? I don't actually know

            //string str = result.Content.ReadAsStringAsync().Result;
            await StartDownload(fs, result, expectedSize);

            // The next thing to figure out is how to get updates on the task
            // Like, after a task is done I want to remove it from the downloads list
            // and while it is downloading I want to update progress.
            // For downloading an image I will probably just await, but bigger files should have some kind of callback
            // for handling progress and task end. Time for some more research!
        }

        private async Task StartDownload(FileStream fileStream, HttpResponseMessage result, long expectedSize)
        {
            await result.Content.CopyToAsync(fileStream);
            fileStream.Close();

            var newFile = new FileInfo(fileStream.Name);
            if (expectedSize != 0 && newFile.Length != expectedSize)
            {
                throw new Exception("Downloaded file doesn't match expected size after download completion: " + newFile.FullName);
            }
            Console.WriteLine("Saving file to " + newFile.FullName);
        }

        /// <summary>
        /// Queries the CDN for all versions of a game
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public async Task<ObservableCollection<uint>> GetVersions(SwitchTitle game)
        {
            //string url = string.Format("https://tagaya.hac.{0}.eshop.nintendo.net/tagaya/hac_versionlist", env);
            string url = string.Format("https://superfly.hac.{0}.d4c.nintendo.net/v1/t/{1}/dv", environment, game.TitleID);
            string r = await GetRequest(url);

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
        public async Task<Dictionary<string,uint>> GetLatestVersions()
        {
            string url = string.Format("https://tagaya.hac.{0}.eshop.nintendo.net/tagaya/hac_versionlist", environment);
            string r = await GetRequest(url).ConfigureAwait(false);

            JObject json = JObject.Parse(r);
            IList<JToken> titles = json["titles"].Children().ToList();

            var result = new Dictionary<string, uint>();
            foreach (var title in titles)
            {
                // Okay so I don't know why (perhaps this has something to do with word alignment? That's too deep in the weeds for me)
                // but every title id ends with 000, except in the results from here they all end with 800
                // Until I understand how it works I'm just going to swap the 8 for a 0.
                // Research update: see get_name_control in python sou rce
                // Titles ending in 000 are base game
                // Titles ending in 800 are updates (what about multiple updates?)
                // I guess this explains why the versions url gets titles ending in 800 - it is a list of updates,
                // and that also explains why titles with no update don't appear there.
                // The pattern for DLC is extra weird
                // TODO: Figure out DLC title ids
                // TODO: Figure out how that 800 works if there are multiple updates - do you do a request for XXX800, plus a version to get only the update file?
                // Does that mean that if you request XXX000 for base title, that the version number is irrelevant? Or do you get updates included in the nsp instead of separately?
                string tid = SwitchTitle.GetBaseGameIDFromUpdate(title.Value<string>("id"));
                uint latestVersion = title.Value<uint>("version");
                result[tid] = latestVersion;
            }
            return result;
        }

        private string GetHeader(HttpResponseHeaders headers, string name)
        {
            if (headers != null && headers.Contains(name))
            {
                IEnumerable<string> h = headers.GetValues(name);
                string result = h.First();
                return result;
            }

            return null;
        }

        private string GetContentHeader(HttpResponseMessage response, string name)
        {
            HttpContentHeaders headers = response?.Content?.Headers;
            if (headers != null && headers.Contains(name))
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
        private async Task<string> GetRequest(string url, X509Certificate cert, Dictionary<string, string> args)
        {
            var response = await MakeRequest(HttpMethod.Get, url, cert, args);
            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Makes a simple reqeust to Ninty's server, using the default client cert and no special headers.
        /// Always a GET request and returns content body as a string.
        /// /// </summary>
        /// <param name="url"></param>
        /// <param name="cert"></param>
        /// <param name="args"></param>
        private async Task<string> GetRequest(string url)
        {
            var response = await MakeRequest(HttpMethod.Get, url, null, null).ConfigureAwait(false);
            string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return result;
        }

        /// <summary>
        /// Makes a request to Ninty's server. Gets back only the response headers.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="cert"></param>
        /// <param name="args"></param>
        private async Task<HttpResponseHeaders> HeadRequest(string url, X509Certificate cert, Dictionary<string, string> args)
        {
            var response = await MakeRequest(HttpMethod.Head, url, cert, args);
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
        private async Task<HttpResponseMessage> MakeRequest(HttpMethod method, string url, X509Certificate cert = null, Dictionary<string, string> args = null)
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
                return await client.SendAsync(request).ConfigureAwait(false);
            }
            // TODO Make http requests async
        }

    }
}