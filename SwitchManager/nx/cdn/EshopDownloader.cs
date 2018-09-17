using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Diagnostics;
using SwitchManager.util;
using System.Security.Cryptography;
using SwitchManager.nx.system;
using System.Text;
using SwitchManager.io;
using log4net;
using System.Threading;

namespace SwitchManager.nx.cdn
{
    public class EshopDownloader
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(EshopDownloader));

        private readonly string environment;
        private readonly string firmware;

        private readonly string deviceId;
        private readonly string region;

        private readonly string imagesPath;
        private readonly string hactoolPath;
        private readonly string keysPath;
        private readonly string clientCertPath;
        private readonly string eShopCertPath;

        private readonly byte[] titleCertTemplateData;
        private readonly byte[] titleTicketTemplateData;

        private string CDNUserAgent { get; set; }

        public int DownloadBuffer { get; set; }

        public X509Certificate ClientCert { get; private set; }
        public X509Certificate EshopCert { get; private set; }

        public EshopDownloader(string clientCertPath, string eShopCertificate, string titleCertPath, string titleTicketPath, string deviceId, string firmware, string environment, string region, string imagesPath, string hactoolPath, string keysPath) :
           this(clientCertPath, eShopCertificate, File.ReadAllBytes(titleCertPath), File.ReadAllBytes(titleTicketPath), deviceId, firmware, environment, region, imagesPath, hactoolPath, keysPath)
        {
        }
        
        public EshopDownloader(string clientCertPath, string eShopCertificate, byte[] certTemplateData, byte[] ticketTemplateData, string deviceId, string firmware, string environment, string region, string imagesPath, string hactoolPath, string keysPath)
        {
            this.clientCertPath = clientCertPath;
            this.eShopCertPath = eShopCertificate;
            this.titleCertTemplateData = certTemplateData;
            this.titleTicketTemplateData = ticketTemplateData;
            this.deviceId = deviceId;
            this.firmware = firmware;
            this.environment = environment;
            this.region = region;
            this.imagesPath = Path.GetFullPath(imagesPath);
            this.hactoolPath = Path.GetFullPath(hactoolPath);
            this.keysPath = Path.GetFullPath(keysPath);
            //this.CDNUserAgent = $"NintendoSDK Firmware/{firmware} (platform:NX; did:{deviceId}; eid:{environment})";
            this.CDNUserAgent = $"NintendoSDK Firmware/{firmware} (platform:NX; eid:{environment})";

            UpdateClientCert(clientCertPath);
            UpdateEshopCert(eShopCertPath);
        }

        /// <summary>
        /// Loads a remote image from nintendo.
        /// 
        /// This is way more complicated and I know I'm gonna need more arguments passed in.
        /// Not implemented for now.
        /// </summary>
        /// <param name="titleID"></param>
        /// <returns></returns>
        public async Task DownloadRemoteImage(SwitchTitle title)
        {
            // Base version or latest version? I'm pretty sure I'm requesting based on a base game title,
            // so the version MUST be BaseVersion. However, some updates can update the icon. That would require
            // going through every possible update, downloading the CNMT, checking if it contains a CONTROL NCA,
            // until you either get a CONTROL NCA or get to the base version.
            // Seems easier to just suck it up and download the base version icon, even if the owlboy icon
            // for version 0 sure looks stupid as hell.
            //uint version = title.LatestVersion ?? await GetLatestVersion(title).ConfigureAwait(false);
            uint version = title.BaseVersion;

            // Locking on a specific title - in the images directory -  which should ensure that none of the same files are accessed
            var @lock = await AquireLock(this.imagesPath, title, version).ConfigureAwait(false);
            try
            {
                // Temporary download folder within the images folder for this title
                // Make sure directory is created first
                string titlePath = this.imagesPath + Path.DirectorySeparatorChar + title.TitleID;
                DirectoryInfo titleDir = Directory.CreateDirectory(titlePath);

                try
                {
                    using (var cnmt = await DownloadAndDecryptCnmt(title, version, titlePath).ConfigureAwait(false))
                    {
                        // Parse "control" type content entries inside the NCA (just one...)
                        // Download each file (just one)

                        string ncaID = cnmt.ParseNCAs(NCAType.Control).First(); // There's only one control.nca
                        string fpath = titlePath + Path.DirectorySeparatorChar + "control.nca";
                        if (await DownloadNCA(ncaID, fpath).ConfigureAwait(false))
                        {
                            Hactool hactool = new Hactool(hactoolPath, keysPath);
                            var controlDir = await hactool.DecryptNCA(fpath).ConfigureAwait(false);

                            DirectoryInfo romfs = controlDir.EnumerateDirectories("romfs").First();

                            var iconFile = romfs.EnumerateFiles("icon_*.dat").First(); // Get all icon files in section0, should just be one
                            iconFile.MoveTo(imagesPath + Path.DirectorySeparatorChar + title.TitleID + ".jpg");

                            var c = GetControlFile(title, romfs.FullName); // this is just to update the name and publisher
                        }
                    }
                }
                finally
                {
                    titleDir.Delete(true);
                }
            }
            finally
            {
                ReleaseLock(@lock);
            }
        }

        private static Dictionary<string, SemaphoreSlim> locks = new Dictionary<string, SemaphoreSlim>();
        private async Task<string> AquireLock(string baseDirectory, SwitchTitle title, uint version)
        {
            string name = baseDirectory + ":" + title.TitleID + ":" + version;
            name = name.Replace(Path.DirectorySeparatorChar, '/');

            SemaphoreSlim l = null;
            lock (locks)
            {
                if (locks.ContainsKey(name))
                    l = locks[name];
                else
                {
                    l = new SemaphoreSlim(1);
                    locks[name] = l;
                }
            }
            await l.WaitAsync().ConfigureAwait(false);
            return name;
        }

        private void ReleaseLock(string name)
        {
            lock (locks)
            {
                if (locks.ContainsKey(name))
                {
                    var l = locks[name];
                    //locks.Remove(name);
                    l.Release();
                }
            }
        }

        /// <summary>
        /// Downloads a title + version from the CDN and repacks it if desired. Verification of downloaded files optional.
        /// </summary>
        /// <param name="title">Title to download (must include titleid and titlekey)</param>
        /// <param name="version">Title version (only applicable to updates, must be a multiple of 0x10000)</param>
        /// <param name="nspRepack">true to pack all downloaded title files into an NSP for later installation</param>
        /// <param name="verify">true to verify the SHA256 of each file with the expected hash and fail if the hashes don't match</param>
        /// <param name="titleDir">Directory to download everything to.</param>
        /// <returns></returns>
        public async Task<NSP> DownloadTitle(SwitchTitle title, uint version, string titleDir, bool nspRepack = false, bool verify = false)
        {
            logger.Info($"Downloading title {title.Name}, ID: {title.TitleID}, VERSION: {version}");

            // Locking one a specific title and version, which should ensure that none of the same files are accessed
            var @lock = await AquireLock(titleDir, title, version).ConfigureAwait(false);

            try
            {
                using (var cnmt = await DownloadAndDecryptCnmt(title, version, titleDir).ConfigureAwait(true))
                {
                    // Now that the CNMT NCA was downloaded and decrypted, read it f
                    string ticketPath = null, certPath = null;
                    if (nspRepack)
                    {
                        string rightsID = $"{title.TitleID}{new String('0', 15)}{cnmt.MasterKeyRevision}";
                        ticketPath = titleDir + Path.DirectorySeparatorChar + rightsID + ".tik";
                        certPath = titleDir + Path.DirectorySeparatorChar + rightsID + ".cert";
                        if (cnmt.Type == TitleType.Application || cnmt.Type == TitleType.AddOnContent)
                        {
                            File.WriteAllBytes(certPath, this.titleCertTemplateData);
                            logger.Info($"Generated title certificate {certPath}.");

                            if (title.IsTitleKeyValid)
                            {
                                // The ticket file starts with the bytes 4 0 1 0, reversed for endianness that gives
                                // 0x00010004, which indicates a RSA_2048 SHA256 signature method.
                                // The signature requires 4 bytes for the type, 0x100 for the signature and 0x3C for padding
                                // The total signature is 0x140. That explains the 0x140 mystery bytes at the start.

                                // Copy the 16-byte value of the 32 character hex title key into memory starting at position 0x180
                                for (int n = 0; n < 0x10; n++)
                                {
                                    string byteValue = title.TitleKey.Substring(n * 2, 2);
                                    this.titleTicketTemplateData[0x180 + n] = byte.Parse(byteValue, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                                }

                                this.titleTicketTemplateData[0x286] = cnmt.MasterKeyRevision;
                                // switchbrew says this should be at 0x285, not 0x286...
                                // Who's right? Does it even matter?

                                // Copy the rights ID in there too at 0x2A0, also 16 bytes (32 characters) long
                                for (int n = 0; n < 0x10; n++)
                                {
                                    string byteValue = rightsID.Substring(n * 2, 2);
                                    this.titleTicketTemplateData[0x2A0 + n] = byte.Parse(byteValue, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
                                }
                                Miscellaneous.HexToBytes(rightsID?.Substring(0, 32), this.titleTicketTemplateData, 0x2A0);
                                File.WriteAllBytes(ticketPath, this.titleTicketTemplateData);

                                logger.Info($"Generated ticket {ticketPath}.");
                            }
                        }
                        else if (cnmt.Type == TitleType.Patch)
                        {
                            // We have to download the CETK file and get the ticket and the certificate from it

                            string cetkPath = $"{titleDir}{Path.DirectorySeparatorChar}{rightsID}.cetk";
                            bool completed = await DownloadCETK(rightsID, cetkPath);
                            if (completed)
                            {
                                using (var cetkStream = File.OpenRead(cetkPath))
                                {
                                    cetkStream.Seek(0x180, SeekOrigin.Begin);
                                    byte[] tkeyBytes = new byte[0x10];
                                    cetkStream.Read(tkeyBytes, 0, 0x10);
                                    title.TitleKey = Miscellaneous.BytesToHex(tkeyBytes);

                                    using (var tikStream = FileUtils.OpenWriteStream(ticketPath))
                                    {
                                        cetkStream.Seek(0, SeekOrigin.Begin);
                                        byte[] tikBytes = new byte[0x2C0];
                                        cetkStream.Read(tikBytes, 0, 0x2C0);
                                        tikStream.Write(tikBytes, 0, 0x2C0);
                                    }

                                    using (var certStream = FileUtils.OpenWriteStream(certPath))
                                    {
                                        cetkStream.Seek(0x2C0, SeekOrigin.Begin);
                                        byte[] certBytes = new byte[0x700];
                                        cetkStream.Read(certBytes, 0, 0x700);
                                        certStream.Write(certBytes, 0, 0x700);
                                    }
                                }
                            }
                        }
                    }

                    List<Task<bool>> tasks = new List<Task<bool>>();

                    // The NSP handles setting up the CNMT as part of the NSP in the constructor
                    // This includes generating the XML, adding the NCA and anything else.
                    NSP nsp = new NSP(title, titleDir, cnmt)
                    {
                        Ticket = ticketPath,
                        Certificate = certPath
                    };

                    // Parse all types except for Meta (which is the CNMT)
                    foreach (var type in new[] { NCAType.Control, NCAType.HtmlDocument, NCAType.LegalInformation, NCAType.Program, NCAType.Data, NCAType.DeltaFragment })
                    {
                        var parsedNCAs = cnmt.ParseContent(type);
                        foreach (var content in parsedNCAs)
                        {
                            string ncaID = content.Key;
                            byte[] hash = verify ? content.Value.HashData : null;

                            // When you add an NCA ID, the NSP generates the proper path using its base directory and the ID
                            string path = nsp.AddNCAByID(type, ncaID);

                            // Dont redownload the cnmt. It wont work anyway, not at this url.
                            if (content.Value.Type != NCAType.Meta)
                            {
                                Task<bool> t = DoDownloadNCA(ncaID, path, hash, title);
                                tasks.Add(t);
                            }
                        }
                    }

                    bool[] results = await Task.WhenAll(tasks);
                    foreach (var r in results)
                    {
                        if (verify && !r)
                        {
                            throw new Exception("At least one NCA failed to verify, NSP repack (if requested) will not continue");
                        }
                        else if (!r)
                        {
                            // Chances are all this means is that it was cancelled
                            // Unfortunately I did cancelling via returning false instead of true,
                            // when really I should have thrown a cancelled exception
                            // Perhaps I will update it some day.
                            logger.Warn("Download didn't complete. It may have been cancelled. NSPs will not be repacked, and you should try the download again later");
                            return null;
                        }
                    }

                    if (cnmt.Type == TitleType.Application)
                    {
                        Hactool hactool = new Hactool(hactoolPath, keysPath);

                        string controlID = cnmt.ParseNCAs(NCAType.Control).First(); // There's only one control.nca
                        string controlPath = titleDir + Path.DirectorySeparatorChar + controlID + ".nca";

                        var ncaDir = await hactool.DecryptNCA(controlPath).ConfigureAwait(false);
                        if (ncaDir != null)
                        {
                            DirectoryInfo romfs = ncaDir.EnumerateDirectories("romfs").First();

                            foreach (var image in romfs.EnumerateFiles("icon_*.dat"))
                            {
                                string name = Path.GetFileNameWithoutExtension(image.Name).Replace("icon_", controlID + ".nx.");
                                string destFile = titleDir + Path.DirectorySeparatorChar + name + ".jpg";
                                if (!File.Exists(destFile))
                                    image.MoveTo(destFile);
                                nsp.AddImage(destFile);
                            }

                            ControlData cdata = GetControlFile(title, romfs.FullName);
                            if (cdata != null)
                            {
                                string controlXmlFile = titleDir + Path.DirectorySeparatorChar + controlID + ".nacp.xml";
                                cdata.GenerateXml(controlXmlFile);
                                nsp.NacpXML = controlXmlFile;
                            }
                            ncaDir.Delete(true);
                        }

                        string legalID = cnmt.ParseNCAs(NCAType.LegalInformation).First(); // There's only one legal.nca
                        string legalPath = titleDir + Path.DirectorySeparatorChar + legalID + ".nca";

                        ncaDir = await hactool.DecryptNCA(legalPath).ConfigureAwait(false);
                        if (ncaDir != null)
                        {
                            DirectoryInfo romfs = ncaDir.EnumerateDirectories("romfs").First();
                            string legalxml = romfs.FullName + Path.DirectorySeparatorChar + "legalinfo.xml";
                            string legalinfoXml = titleDir + Path.DirectorySeparatorChar + legalID + ".legalinfo.xml";
                            if (File.Exists(legalxml))
                            {
                                File.Copy(legalxml, legalinfoXml, true);
                                nsp.LegalinfoXML = legalinfoXml;
                            }
                            ncaDir.Delete(true);
                        }

                        // TODO unpack and parse program nca, generate *.programinfo.xml

                    }

                    // Repack to NSP if requested AND if the title has a key
                    if (nspRepack && title.IsTitleKeyValid)
                    {
                        return nsp;
                    }
                    else
                    {
                        return null;
                    }
                }
            }
            finally
            {
                ReleaseLock(@lock);
            }
        }

        /// <summary>
        /// Gets and parses the control.nacp file. You must provide the path to the romfs directory of
        /// an unpacked Control NCA.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="romfsDirectory"></param>
        /// <returns></returns>
        private ControlData GetControlFile(SwitchTitle title, string romfsDirectory)
        {
            string controlFile = romfsDirectory + Path.DirectorySeparatorChar + "control.nacp";
            if (File.Exists(controlFile))
            {
                ControlData cdata = ControlData.Parse(controlFile);
                if (cdata == null) return null;

                for (int i = 0; i < cdata.Titles.Length; i++)
                {
                    var ct = cdata.Titles[i];
                    if (ct != null)
                    {
                        title.Name = ct.Name;
                        title.Publisher = ct.Publisher;
                        break;
                    }
                }
                return cdata;
            }
            return null;
        }

        private async Task<bool> DoDownloadNCA(string ncaID, string path, byte[] expectedHash, SwitchTitle title = null)
        {
            logger.Info($"Downloading NCA {ncaID}.");
            bool completed = await DownloadNCA(ncaID, path, title).ConfigureAwait(false);
            if (!completed) return false;

            // A null hash means no verification necessary, just return true
            if (expectedHash != null)
            {
                return Crypto.VerifySha256Hash(path, expectedHash);
            }
            return true;
        }

        private async Task<bool> DownloadNCA(string ncaID, string path, SwitchTitle title = null)
        {
            string url = $"https://atum.hac.{environment}.d4c.nintendo.net/c/c/{ncaID}?device_id={deviceId}";

            return await DownloadFile(url, path, title?.Name).ConfigureAwait(false); // download file and wait for it since we can't do anything until it is done
        }

        internal void UpdateClientCert(string clientCertPath)
        {
            this.ClientCert = Crypto.LoadCertificate(clientCertPath);
        }

        internal void UpdateEshopCert(string clientCertPath)
        {
            this.EshopCert = Crypto.LoadCertificate(eShopCertPath);
        }

        public async Task<long> GetContentLength(string url)
        {
            var result = await CDNRequest(HttpMethod.Get, url, null, false).ConfigureAwait(false);
            long cLength = result.Content.Headers.ContentLength ?? 0;
            return cLength;
        }

        /// <summary>
        /// Downloads a file from Nintendo and saves it to the specified path.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="fpath"></param>
        public async Task<bool> DownloadFile(string url, string fpath, string jobName = null)
        {
            downloadTasks.TryGetValue(fpath, out Task<bool> task);
            if (task == null)
            {
                long downloaded = 0;
                long expectedSize = 0;
                FileStream fs;
                HttpResponseMessage result;

                if (File.Exists(fpath))
                {
                    downloaded = FileUtils.GetFileSystemSize(fpath) ?? 0;

                    result = await CDNRequest(HttpMethod.Get, url, new Dictionary<string, string>() { { "Range", "bytes=" + downloaded + "-" } }, false).ConfigureAwait(false);

                    if (!result.Headers.Server.First().ToString().Equals("openresty/1.9.7.4")) // Completed download
                    {
                        logger.Info("Download complete, skipping: " + fpath);
                        return true;
                    }
                    else if (result.Content.Headers.ContentRange == null) // CDN doesn't return a range if request >= filesize
                    {
                        long cLength = result.Content.Headers.ContentLength ?? 0;
                        expectedSize = cLength;
                    }
                    else
                    {
                        long cLength = result.Content.Headers.ContentLength ?? 0;
                        expectedSize = downloaded + cLength;
                    }

                    if (downloaded == expectedSize)
                    {
                        logger.Info("Download complete, skipping: " + fpath);
                        return true;
                    }
                    else if (downloaded < expectedSize)
                    {
                        logger.Info("Resuming previous download: " + fpath);
                        fs = FileUtils.OpenWriteStream(fpath, true);
                    }
                    else
                    {
                        logger.Warn("Existing file is larger than it should be, restarting: " + fpath);
                        downloaded = 0;
                        fs = File.Create(fpath);
                    }

                }
                else
                {
                    fs = File.Create(fpath);
                    downloaded = 0;

                    result = await CDNRequest(HttpMethod.Get, url, null, false).ConfigureAwait(false);
                    long cLength = result.Content.Headers.ContentLength ?? 0;
                    expectedSize = cLength;
                }

                task = StartDownload(fs, result, expectedSize, downloaded, jobName).ContinueWith(a =>
                {
                    bool done = a.Result;
                    fs.Dispose();
                    result.Dispose();

                    var newFile = new FileInfo(fpath);
                    if (done && expectedSize != 0 && newFile.Length != expectedSize)
                    {
                        throw new Exception("Downloaded file doesn't match expected size after download completion: " + newFile.FullName);
                    }
                    downloadTasks.Remove(fpath);
                    return done;
                });
                downloadTasks.Add(fpath, task);
            }

            bool completed = await task.ConfigureAwait(false);
            return completed;
        }

        Dictionary<string, Task<bool>> downloadTasks = new Dictionary<string, Task<bool>>();

        /// <summary>
        /// Starts an async file download, which downloads the file in chunks and reports progress, as well
        /// as the start and end of the download.
        /// </summary>
        /// <param name="fileStream"></param>
        /// <param name="result"></param>
        /// <param name="expectedSize"></param>
        /// <returns>true if the download completed and false if it was cancelled</returns>
        private async Task<bool> StartDownload(FileStream fileStream, HttpResponseMessage result, long expectedSize, long startingSize = 0, string jobName = null)
        {
            using (Stream remoteStream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                DownloadJob job = new DownloadJob(remoteStream, fileStream, jobName, expectedSize, startingSize);
                job.Start();

                byte[] buffer = new byte[this.DownloadBuffer];
                while (true)
                {
                    // Read from the web.
                    int n = await remoteStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

                    if (n == 0 || job.IsCancelled)
                    {
                        // There is nothing else to read.
                        break;
                    }

                    // Report progress.
                    job.UpdateProgress(n);

                    // Write to file.
                    fileStream.Write(buffer, 0, n);
                }
                job.Finish();
                fileStream.Flush();

                return !job.IsCancelled;
            }
        }

        /// <summary>
        /// Gets the CETK (perhaps content entry title key?). It contains the title key for updates.
        /// </summary>
        /// <param name="rightsID"></param>
        /// <param name="fpath"></param>
        /// <returns></returns>
        private async Task<bool> DownloadCETK(string rightsID, string fpath)
        {
            string url = $"https://atum.hac.{environment}.d4c.nintendo.net/r/t/{rightsID}?device_id={deviceId}";
            var head = await CDNHead(url).ConfigureAwait(false);

            string cnmtid = GetHeader(head, "X-Nintendo-Content-ID");

            url = $"https://atum.hac.{environment}.d4c.nintendo.net/c/t/{cnmtid}?device_id={deviceId}";
            return await DownloadFile(url, fpath).ConfigureAwait(false);
        }

        /// <summary>
        /// Queries the CDN for all versions of a game
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public async Task<uint> GetLatestVersion(SwitchTitle game)
        {
            //string url = string.Format("https://tagaya.hac.{0}.eshop.nintendo.net/tagaya/hac_versionlist", env);
            string url = string.Format("https://superfly.hac.{0}.d4c.nintendo.net/v1/t/{1}/dv", environment, game.TitleID);
            string r = await CDNGet(url).ConfigureAwait(false);

            JObject json = JObject.Parse(r);
            uint latestVersion = json?.Value<uint>("version") ?? 0;

            return latestVersion;
        }

        /// <summary>
        /// Gets ALL games' versions and required versions, whatever that means.
        /// format is {"format_version":1,"last_modified":1533248100}, "titles":[{"id":"01007ef00011e800","version":720896,"required_version":720896},...]}
        /// 
        /// Versions are 0, 0x10000, 0x20000, etc up to the listed number.
        /// </summary>
        /// <returns></returns>
        public async Task<Dictionary<string, uint>> GetLatestVersions()
        {
            string url = string.Format("https://tagaya.hac.{0}.eshop.nintendo.net/tagaya/hac_versionlist", environment);
            string r = await CDNGet(url).ConfigureAwait(false);

            JObject json = JObject.Parse(r);

            IList<JToken> titles = json["titles"].Children().ToList();

            var result = new Dictionary<string, uint>();
            foreach (var title in titles)
            {
                string tid = title.Value<string>("id");
                tid = SwitchTitle.GetBaseGameIDFromUpdate(tid);
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

        /// <summary>
        /// Makes a simple reqeust to Ninty's server, using the default client cert and no special headers.
        /// Always a GET request and returns content body as a string.
        /// /// </summary>
        /// <param name="url"></param>
        /// <param name="cert"></param>
        /// <param name="args"></param>
        private async Task<string> CDNGet(string url, bool waitForContent = true, Dictionary<string, string> args = null)
        {
            var response = await CDNRequest(HttpMethod.Get, url, args, waitForContent).ConfigureAwait(false);
            string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (result.Contains("Access Denied"))
                throw new CertificateDeniedException();
            return result;
        }

        /// <summary>
        /// Makes a request to Ninty's server. Gets back only the response headers.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="cert"></param>
        /// <param name="args"></param>
        private async Task<HttpResponseHeaders> CDNHead(string url, Dictionary<string, string> args = null)
        {
            var response = await CDNRequest(HttpMethod.Head, url, args).ConfigureAwait(false);
            return response.Headers;
        }

        /// <summary>
        /// Makes a POST request to Ninty's server.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="cert"></param>
        /// <param name="args"></param>
        private async Task<HttpResponseMessage> EshopPost(string url, HttpContent payload, Dictionary<string, string> args = null)
        {
            payload.Headers.Add("Host", $"dauth-{environment}.ndas.srv.nintendo.net");
            payload.Headers.Add("User-Agent", "libcurl (nnDauth; 789f928b - 138e-4b2f - afeb - 1acae821d897; SDK 5.3.0.0; Add - on 5.3.0.0)");
            payload.Headers.Add("Accept", "*/*");
            if (args != null) args.ToList().ForEach(x => payload.Headers.Add(x.Key, x.Value));

            var client = GetSingletonClient(ClientCert);
            return await client.PostAsync(url, payload).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes a POST request to Ninty's server.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="cert"></param>
        /// <param name="args"></param>
        private async Task<HttpResponseMessage> EshopPost(string url, Dictionary<string, string> payload, Dictionary<string, string> args = null)
        {
            var content = new FormUrlEncodedContent(payload);

            return await EshopPost(url, content, args).ConfigureAwait(false);
        }

        private HttpClientHandler singletonHandler;
        private HttpClient singletonClient;
        public HttpClient GetSingletonClient(X509Certificate cert)
        {
            if (singletonClient == null)
            {
                // Add the client certificate
                singletonHandler = new HttpClientHandler
                {
                    ClientCertificateOptions = ClientCertificateOption.Manual,
                    //SslProtocols = SslProtocols.Tls12,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
                };
                ServicePointManager.ServerCertificateValidationCallback += (o, c, ch, er) => true;
                ServicePointManager.DefaultConnectionLimit = 1000;

                // Create client and get response
                singletonClient = new HttpClient(singletonHandler);
                singletonClient.Timeout = TimeSpan.FromMinutes(30);
            }

            singletonHandler.ClientCertificates.Clear();
            singletonHandler.ClientCertificates.Add(cert);

            return singletonClient;
        }

        /// <summary>
        /// Makes a request to Ninty's server. Gets back the entire response.
        /// WHY THE FUCK IS THIS SO COMPLICATED???? JUST LET ME SEND A REQUEST
        /// </summary>
        /// <param name="method"></param>
        /// <param name="url"></param>
        /// <param name="cert"></param>
        /// <param name="args"></param>
        private async Task<HttpResponseMessage> CDNRequest(HttpMethod method, string url, Dictionary<string, string> args = null, bool waitForContent = true)
        {
            var headers = new Dictionary<string, string>()
            {
                { "User-Agent", CDNUserAgent },
                { "Accept-Encoding", "gzip, deflate" },
                { "Accept", "*/*" },
                { "Connection", "keep-alive" },
            };
            if (args != null) args.ToList().ForEach(x => headers.Add(x.Key, x.Value));
            return await WebRequest(method, url, ClientCert, headers, waitForContent).ConfigureAwait(false);
        }

        /// <summary>
        /// Makes a request to Ninty's server. Gets back the entire response.
        /// WHY THE FUCK IS THIS SO COMPLICATED???? JUST LET ME SEND A REQUEST
        /// </summary>
        /// <param name="method"></param>
        /// <param name="url"></param>
        /// <param name="cert"></param>
        /// <param name="args"></param>
        private async Task<HttpResponseMessage> WebRequest(HttpMethod method, string url, X509Certificate cert, Dictionary<string, string> args = null, bool waitForContent = true)
        {
            // Create request with method & url, then add headers
            var request = new HttpRequestMessage(method, url);
            // Add any additional parameters passed into the method
            if (args != null) args.ToList().ForEach(x => request.Headers.Add(x.Key, x.Value));

            var client = GetSingletonClient(cert);
            if (waitForContent)
                return await client.SendAsync(request, HttpCompletionOption.ResponseContentRead).ConfigureAwait(false);
            else
                return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
        }

        /// <summary>
        /// Get a game's CNMT ID, which you can use to download the CNMT NCA.
        /// </summary>
        /// <param name="title">Title to get the CNMD ID for.</param>
        /// <param name="version">Version of the title you want. ` </param>
        /// <returns></returns>
        private async Task<string> GetCnmtID(SwitchTitle title, uint version)
        {
            string url = $"https://atum.hac.{environment}.d4c.nintendo.net/t/a/{title.TitleID}/{version}?device_id={deviceId}";

            var head = await CDNHead(url).ConfigureAwait(false);

            string cnmtid = GetHeader(head, "X-Nintendo-Content-ID");
            
            return cnmtid;
        }

        /// <summary>
        /// Downloads a CNMT NCA file from Nintendo's CDN.
        /// </summary>
        /// <param name="cnmtid">ID of the CNMT. Use GetCnmtId to find it.</param>
        /// <param name="path">Path of the downloaded file. This is where it will be once this function is completed.</param>
        /// <returns>FileInfo for the downloaded CNMT NCA.</returns>
        private async Task<bool> DownloadCnmt(string cnmtid, string path)
        {
            // Download cnmt file, async
            string url = $"https://atum.hac.{environment}.d4c.nintendo.net/c/a/{cnmtid}?device_id={deviceId}";
            return await DownloadFile(url, path).ConfigureAwait(false);
        }

        private async Task<CNMT> DownloadAndDecryptCnmt(SwitchTitle title, uint version, string titleDir)
        {
            // Get the CNMT ID for the title
            string cnmtid = await GetCnmtID(title, version).ConfigureAwait(false);
            if (cnmtid == null)
                throw new CnmtMissingException($"No or invalid CNMT ID found for {title.Name} {title.TitleID}");

            // Path to the NCA
            string ncaPath = titleDir + Path.DirectorySeparatorChar + cnmtid + ".cnmt.nca";

            // Download the CNMT NCA file
            bool completed = await DownloadCnmt(cnmtid, ncaPath).ConfigureAwait(false);
            if (!completed) return null;

            // Decrypt the CNMT NCA file (all NCA files are encrypted by nintendo)
            // Hactool does the job for us
            Hactool hactool = new Hactool(hactoolPath, keysPath);
            DirectoryInfo cnmtDir = await hactool.DecryptNCA(ncaPath).ConfigureAwait(false);

            CNMT cnmt = GetDownloadedCnmt(cnmtDir, ncaPath);
            return cnmt;
        }

        private CNMT GetDownloadedCnmt(DirectoryInfo cnmtDir, string ncaPath)
        {
            // For CNMTs, there is a section0 containing a single cnmt file, plus a Header.bin right next to section0
            var sectionDirInfo = cnmtDir.EnumerateDirectories("section0").First();
            var extractedCnmt = sectionDirInfo.EnumerateFiles().First();
            var headerFile = cnmtDir.EnumerateFiles("Header.bin").First();

            return new CNMT(extractedCnmt.FullName, headerFile.FullName, cnmtDir.FullName, ncaPath);
        }

        /// <summary>
        /// Eshop requests do not work. I suspect it has something to do with sending them a bearer token,
        /// but I have no idea how to get that, short of trying to log in with my own account and copying the token,
        /// and I don't want to do that.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="lang"></param>
        /// <returns></returns>
        public async Task<HttpResponseMessage> GetEshopData(EshopLogin token, SwitchTitle title, string lang)
        {
            string url = $"https://bugyo.hac.{environment}.eshop.nintendo.net/shogun/v1/contents/ids?shop_id=4&lang={lang}&country={region}&type=title&title_ids={title.TitleID}";

            var response = await WebRequest(HttpMethod.Get, url, EshopCert).ConfigureAwait(false);

            return response;
        }

        private static byte[] MasterKey = { 0xCF, 0xA2, 0x17, 0x67, 0x90, 0xA5, 0x3F, 0xF7, 0x49, 0x74, 0xBF, 0xF2, 0xAF, 0x18, 0x09, 0x21 };
        private static byte[] AESUseSrc = { 0x4D, 0x87, 0x09, 0x86, 0xC4, 0x5D, 0x20, 0x72, 0x2F, 0xBA, 0x10, 0x53, 0xDA, 0x92, 0xE8, 0xA9 };
        private static byte[] DAuth_KEK = { 0x8B, 0xE4, 0x5A, 0xBC, 0xF9, 0x87, 0x02, 0x15, 0x23, 0xCA, 0x4F, 0x5E, 0x23, 0x00, 0xDB, 0xF0 };
        private static byte[] DAuth_Src = { 0xDE, 0xD2, 0x4C, 0x35, 0xA5, 0xD8, 0xC0, 0xD7, 0x6C, 0xB8, 0xD7, 0x8C, 0xA0, 0xA5, 0xA5, 0x22 };
        public static string SysDigest = "gW93A#00050100#29uVhARHOdeTZmfdPnP785egrfRbPUW5n3IAACuHoPw=";

        public async Task<EshopLogin> EshopLogin()
        {
            string clientId = "93af0acb26258de9"; // whats this? device id or different?
            //string clientId2 = "81333c548b2e876d";

            string challengeUrl = $"https://dauth-{environment}.ndas.srv.nintendo.net/v3-59ed5fa1c25bb2aea8c4d73d74b919a94d89ed48d6865b728f63547943b17404/challenge";
            string deviceAuthTokenUrl = $"https://dauth-{environment}.ndas.srv.nintendo.net/v3-59ed5fa1c25bb2aea8c4d73d74b919a94d89ed48d6865b728f63547943b17404/device_auth_token";

            var response = await EshopPost(challengeUrl, new Dictionary<string, string>() { { "key_generation", "5" } }).ConfigureAwait(false);
            string text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            JObject json = JObject.Parse(text);

            string base64data = json?.Value<string>("data");
            string challenge = json?.Value<string>("challenge");

            byte[] keySource = Crypto.DecodeBase64(base64data);
            var kek = Crypto.GenerateAESKek(MasterKey, AESUseSrc, DAuth_KEK, keySource);

            string req = $"challenge={Uri.EscapeDataString(challenge)}&client_id={Uri.EscapeDataString(clientId)}&key_generation=5&system_version={Uri.EscapeDataString(SysDigest)}";

            byte[] cmacData = Crypto.AESCMAC(kek, Encoding.UTF8.GetBytes(req));
            string base64Cmac = Crypto.EncodeBase64(cmacData);
            string mac = base64Cmac.Replace("+", "-").Replace("/", "_").Replace("=", "");
            req += $"&mac={Uri.EscapeDataString(mac)}";

            /*
            Dictionary<string, string> payload = new Dictionary<string, string>();
            payload.Add("challenge", challenge);
            payload.Add("client_id", clientId);
            payload.Add("key_generation", "5");
            payload.Add("system_version", "gW93A#00050100#29uVhARHOdeTZmfdPnP785egrfRbPUW5n3IAACuHoPw=");

            var content = new FormUrlEncodedContent(payload);
            */
            var content = new StringContent(req);
            response = await EshopPost(deviceAuthTokenUrl, content).ConfigureAwait(false);
            text = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            json = JObject.Parse(text);
            string token = json?.Value<string>("device_auth_token");
            EshopLogin l = new EshopLogin();
            l.Token = token;
            return l;
        }

        /// <summary>
        /// Gets the estimated size of a title. The only file downloaded is the CNMT. All of the files that would go
        /// into the NSP are summed up, using all available information (including the sizes in the CNMT). This requires
        /// generating the CNMT xml file.
        /// </summary>
        /// <param name="title"></param>
        /// <param name="version"></param>
        /// <param name="titleDir"></param>
        /// <returns></returns>
        public async Task<long> GetTitleSize(SwitchTitle title, uint version, string titleDir)
        {
            // Locking one a specific title and version - in the title directory -  which should ensure that none of the same files are accessed
            // I noticed some crazy errors that came up because the event that fires on grid details visibility changed was firing
            // multiple times very fast, which (I think) meant that the same files were getting written to by two different worker threads at once
            // I fixed that bug by ensuring that it only fired once when the details area appeared,
            // but the underlying problem is that the files aren't being locked. Thus, this mutex locks the entire
            // method, but only for a specific directory that is being worked on
            // This mutex will generally be shared by multiple calls on this method and on DownloadTitle,
            // since they both have the potential to write to the same files
            var @lock = await AquireLock(titleDir, title, version).ConfigureAwait(false);
            try
            {
                using (var cnmt = await DownloadAndDecryptCnmt(title, version, titleDir).ConfigureAwait(false))
                {
                    var cnmtSize = FileUtils.GetFileSystemSize(cnmt.CnmtFilePath) ?? 0;

                    var ticketSize = this.titleTicketTemplateData?.Length ?? 0;
                    var certSize = this.titleCertTemplateData?.Length ?? 0;

                    string cnmtXml = Path.GetFullPath(cnmt.CnmtNcaFilePath).Replace(".nca", ".xml");
                    cnmt.GenerateXml(cnmtXml);
                    var cnmtXmlSize = FileUtils.GetFileSystemSize(cnmtXml) ?? 0;

                    var parsedNCAFiles = cnmt.ParseContent();
                    var files = new List<string>();
                    var sizes = new List<long>();
                    string controlID = null;
                    string controlPath = null;

                    // The size of an NSP includes all of the above files, but also the NCAs and the header
                    // that lists the NCAs (and other files). The list of files must be compiled to generate an accurate header,
                    // and the list of sizes is needed both to generate the header and to sum up to the total
                    foreach (var nca in parsedNCAFiles)
                    {
                        files.Add(titleDir + Path.DirectorySeparatorChar + nca.Key + ".nca");

                        if (cnmt.Type == TitleType.AddOnContent)
                        {
                            string url = $"https://atum.hac.{environment}.d4c.nintendo.net/c/c/{nca.Key}?device_id={deviceId}";
                            long size = await this.GetContentLength(url).ConfigureAwait(false);
                            sizes.Add(size);
                        }
                        else
                        {
                            sizes.Add(nca.Value.Size);
                        }

                        if (nca.Value.Type == NCAType.Control)
                        {
                            controlID = nca.Key;
                            controlPath = titleDir + Path.DirectorySeparatorChar + controlID + ".nca";
                            await DownloadNCA(controlID, controlPath).ConfigureAwait(false);
                        }
                    }

                    files.Add(cnmt.CnmtFilePath); sizes.Add(cnmtSize);
                    files.Add(cnmtXml); sizes.Add(cnmtXmlSize);

                    string rightsID = $"{title.TitleID}{new String('0', 15)}{cnmt.MasterKeyRevision}";
                    files.Add(titleDir + Path.DirectorySeparatorChar + rightsID + ".tik"); sizes.Add(certSize);
                    files.Add(titleDir + Path.DirectorySeparatorChar + rightsID + ".cert"); sizes.Add(ticketSize);

                    // Extract the images and add their file names and sizes
                    if (controlPath != null)
                    {
                        Hactool hactool = new Hactool(hactoolPath, keysPath);
                        var controlDir = await hactool.DecryptNCA(controlPath).ConfigureAwait(false);
                        try
                        {
                            var dirs = controlDir.EnumerateDirectories("romfs");
                            if (dirs.Count() == 1)
                            {
                                var romfs = dirs.First();
                                // Check for directory named romfs and, if it exists, there is only one and it is the first
                                foreach (var image in romfs.EnumerateFiles("icon_*.dat"))
                                {
                                    string destFile = titleDir + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(image.Name) + ".jpg";
                                    files.Add(destFile);
                                    sizes.Add(FileUtils.GetFileSystemSize(destFile) ?? 0);
                                }
                                GetControlFile(title, romfs.FullName);
                            }
                        }
                        finally
                        {
                            FileUtils.DeleteDirectory(controlDir, true);
                        }
                    }


                    // Add up the sizes of all the files plus the NSP header
                    return sizes.Sum() + NSP.GenerateHeader(files.ToArray(), sizes.ToArray()).Length;
                }
            }
            finally
            {
                ReleaseLock(@lock);
            }
        }
    }
}
                