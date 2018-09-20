using log4net;
using SwitchManager.io;
using SwitchManager.util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.system
{
    public class NSP
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(NSP));

        private Dictionary<NCAType, List<string>> NCAs = new Dictionary<NCAType, List<string>>();

        public List<string> NcaFiles
        {
            get
            {
                return NCAs.SelectMany(e => e.Value).ToList();
            }
        }

        public long TotalSize
        {
            get
            {
                return HeaderSize + FilesSize;
            }
        }

        public long FilesSize
        {
            get
            {
                return this.Files.Sum((s) => s == null ? 0 : FileUtils.GetFileSystemSize(s) ?? 0);
            }
        }

        public long HeaderSize
        {
            get
            {
                return GenerateHeader(this.Files.ToArray()).Length;
            }
        }

        public List<string> Files
        {
            get
            {
                List<string> files = new List<string> { Certificate };

                if (!string.IsNullOrWhiteSpace(Title.TitleKey)) files.Add(Ticket);

                files.AddRange(NcaFiles);

                if (!string.IsNullOrWhiteSpace(CnmtXML)) files.Add(CnmtXML);
                if (!string.IsNullOrWhiteSpace(LegalinfoXML)) files.Add(LegalinfoXML);
                if (!string.IsNullOrWhiteSpace(PrograminfoXML)) files.Add(PrograminfoXML);
                if (!string.IsNullOrWhiteSpace(ControlXML)) files.Add(ControlXML);

                files.AddRange(this.IconFiles);
                files.AddRange(this.miscFiles);

                return files;
            }
        }
        
        public SwitchTitle Title { get; set; }

        // The ticket and cert files, .tik and .cert
        public string Certificate { get; set; }
        public string Ticket { get; set; }

        // The meta NCA, I keep it here because it is special
        public string CnmtNCA { get; set; }

        // Four types of XML files - .cnmt.xml, .programinfo.xml, .legalinfo.xml and .nacp.xml
        public string CnmtXML { get; set; }
        public string PrograminfoXML { get; set; }
        public string LegalinfoXML { get; set; }
        public string ControlXML { get; set; }

        // Images/icons from the control file
        public List<string> IconFiles { get; private set; } = new List<string>();
        public CNMT CNMT { get; internal set; }

        // Any other unknown files, since NSPs can hold anything
        private List<string> miscFiles = new List<string>();

        public string Directory { get; set; }

        public NSP(SwitchTitle title, string baseDirectory, CNMT cnmt)
        {
            this.Title = title;
            this.CnmtNCA = cnmt.CnmtNcaFilePath;
            this.CnmtXML = cnmt.GenerateXml();
            this.Directory = baseDirectory;

            this.CNMT = cnmt;
            AddNCAFile(NCAType.Meta, CnmtNCA);
        }

        public NSP(string baseDirectory)
        {
            // All fields must be added manually
            this.Directory = baseDirectory;
        }

        /// <summary>
        /// Repacks this NSP from a set of files into a single file and writes the file out to the given path.
        /// </summary>
        /// <param name="path"></param>
        public async Task<bool> Repack(string path)
        {
            logger.Info($"Repacking to NSP file {path}.");

            string[] files = this.Files.ToArray();
            int nFiles = files.Length;


            var hd = GenerateHeader(files);

            // Use lambda to sum sizes of all files in files array
            long totalSize = this.FilesSize + hd.Length;

            FileInfo finfo = new FileInfo(path);
            if (finfo.Exists && finfo.Length == totalSize)
            {
                logger.Warn($"NSP already exists {path}. Delete and try again.");
                return true;
            }

            using (JobFileStream str = new JobFileStream(path, "NSP repack of " + Title.ToString(), totalSize, 0))
            {
                await str.WriteAsync(hd, 0, hd.Length).ConfigureAwait(false);
                // Copy each file to the end of the NSP in sequence. Nothing special here just copy them all.
                foreach (var file in files)
                {
                    using (FileStream fs = File.OpenRead(file))
                        await str.CopyFromAsync(fs).ConfigureAwait(false);
                }
            }

            finfo.Refresh();
            if (finfo.Exists && finfo.Length == totalSize)
            {
                logger.Info($"Successfully repacked NSP to {path}");
                return true;
            }

            logger.Error($"Failed to repack NSP to {path}");
            return false;
        }

        public static byte[] GenerateHeader(string[] files)
        {
            // Calculate the file sizes array (size of file for each file)
            var fileSizes = files.Select(f => new FileInfo(f).Length).ToArray();
            return GenerateHeader(files, fileSizes);
        }

        /// <summary>
        /// Generates the PFS0 (NSP) header
        /// See http://switchbrew.org/index.php?title=NCA_Format#PFS0
        /// </summary>
        /// <returns></returns>
        public static byte[] GenerateHeader(string[] files, long[] fileSizes)
        {
            int nFiles = files.Length;

            // The size of the header is 0x10, plus one 0x18 size entry for each file, plus the size of the string table
            // The string table is all of the file names, terminated by nulls
            int stringTableSize = files.Sum((s) => Path.GetFileName(s).Length + 1);
            int headerSize = 0x10 + (nFiles) * 0x18 + stringTableSize;

            int remainder = 0x10 - headerSize % 0x10;
            headerSize += remainder;

            // Calculate the file offsets array (offset of file from start for each file)
            var fileOffsets = new long[nFiles];
            for (int i = 0; i < fileOffsets.Length; i++) // fileOffsets = [sum(fileSizes[:n]) for n in range(filesNb)]
                for (int j = 0; j < i; j++)
                    fileOffsets[i] += fileSizes[j];

            // Calculate the filename lengths array (length of file names)
            var fileNamesLengths = files.Select(f => Path.GetFileName(f).Length + 1).ToArray(); // fileNamesLengths = [len(os.path.basename(file))+1 for file in self.files] # +1 for the \x00

            var stringTableOffsets = new int[nFiles];
            for (int i = 0; i < stringTableOffsets.Length; i++) // = [sum(fileNamesLengths[:n]) for n in range(filesNb)]
                for (int j = 0; j < i; j++)
                    stringTableOffsets[i] += fileNamesLengths[j];

            byte[] header = new byte[headerSize];
            int n = 0;

            // 0x0 + 0x4 PFS0 magic number
            header[n++] = unchecked('P' & 0xFF);
            header[n++] = unchecked('F' & 0xFF);
            header[n++] = unchecked('S' & 0xFF);
            header[n++] = unchecked('0' & 0xFF);

            // 0x4 + 0x4 number of files
            byte[] nfBytes = BitConverter.GetBytes(nFiles); nfBytes.CopyTo(header, n); n += nfBytes.Length;

            // 0x8 + 0x4 (plus remainder so it reaches a multple of 0x10) size of string table
            byte[] stBytes = BitConverter.GetBytes(stringTableSize + remainder); stBytes.CopyTo(header, n); n += stBytes.Length;

            // 0xC + 0x4 Zero/Reserved
            header[n++] = header[n++] = header[n++] = header[n++] = 0x00;

            // 0x10 + 0x18 * nFiles File Entry Table
            // One File Entry for each file
            for (int i = 0; i < nFiles; i++)
            {
                // 0x0 + 0x8 Offset of this file from start of file data block
                byte[] foBytes = BitConverter.GetBytes(fileOffsets[i]); foBytes.CopyTo(header, n); n += foBytes.Length;

                // 0x8 + 0x8 Size of this specific file within the file data block
                byte[] fsBytes = BitConverter.GetBytes(fileSizes[i]); fsBytes.CopyTo(header, n); n += fsBytes.Length;

                // 0x10 + 0x4 Offset of this file's filename within the string table
                byte[] stoBytes = BitConverter.GetBytes(stringTableOffsets[i]); stoBytes.CopyTo(header, n); n += stoBytes.Length;

                // 0x14 + 0x4 Zero?
                header[n++] = header[n++] = header[n++] = header[n++] = 0x00;
            }

            // (0x10 + X) + Y string table, where X is file table size and Y is string table size
            // Encode every string in UTF8, then terminate with a 0
            foreach (var str in files)
            {
                byte[] strBytes = Encoding.UTF8.GetBytes(Path.GetFileName(str)); strBytes.CopyTo(header, n); n += strBytes.Length;
                header[n++] = 0;
            }

            while (remainder-- > 0)
                header[n++] = 0;

            return header;
        }

        /// <summary>
        /// </summary>
        /// <param name="path"></param>
        public static async Task<NSP> ParseNSP(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                logger.Error("Empty path passed to NSP.Unpack.");
                return null;
            }

            FileInfo finfo = new FileInfo(path);
            if (!finfo.Exists)
            {
                logger.Error($"Non-existent file passed to NSP.Unpack: {path}");
                return null;
            }
            
            using (JobFileStream nspReadStream = new JobFileStream(path, "NSP unpack of " + path, finfo.Length, 0))
            {
                using (BinaryReader br = new BinaryReader(nspReadStream))
                {
                    if (br.ReadChar() != 'P') throw new InvalidNspException("Wrong header");
                    if (br.ReadChar() != 'F') throw new InvalidNspException("Wrong header");
                    if (br.ReadChar() != 'S') throw new InvalidNspException("Wrong header");
                    if (br.ReadChar() != '0') throw new InvalidNspException("Wrong header");

                    // 0x4 + 0x4 number of files
                    int numFiles = br.ReadInt32();
                    if (numFiles < 1) throw new InvalidNspException("No files inside NSP");

                    // 0x8 + 0x4  size of string table (plus remainder so it reaches a multple of 0x10)
                    int stringTableSize = br.ReadInt32();
                    if (stringTableSize < 1) throw new InvalidNspException("Invalid or zero string table size");

                    // 0xC + 0x4 Zero/Reserved
                    br.ReadUInt32();

                    long[] fileOffsets = new long[numFiles];
                    long[] fileSizes = new long[numFiles];
                    int[] stringTableOffsets = new int[numFiles];

                    // 0x10 + 0x18 * nFiles File Entry Table
                    // One File Entry for each file
                    for (int i = 0; i < numFiles; i++)
                    {
                        // 0x0 + 0x8 Offset of this file from start of file data block
                        fileOffsets[i] = br.ReadInt64();

                        // 0x8 + 0x8 Size of this specific file within the file data block
                        fileSizes[i] = br.ReadInt64();

                        // 0x10 + 0x4 Offset of this file's filename within the string table
                        stringTableOffsets[i] = br.ReadInt32();

                        // 0x14 + 0x4 Zero?
                        br.ReadInt32();
                    }

                    // (0x10 + X) + Y string table, where X is file table size and Y is string table size
                    // Encode every string in UTF8, then terminate with a 0
                    byte[] strBytes = br.ReadBytes(stringTableSize);
                    var files = new string[numFiles];
                    for (int i = 0; i < numFiles; i++)
                    {
                        // Start of the string is in the string table offsets table
                        int thisOffset = stringTableOffsets[i];

                        // Decode UTF8 string and assign to files array
                        string name = strBytes.DecodeUTF8NullTerminated(thisOffset);
                        //string name = Encoding.UTF8.GetString(strBytes, thisOffset, thisLength);
                        files[i] = name;
                    }

                    // The header is always aligned to a multiple of 0x10 bytes
                    // It is padded with 0s until the header size is a multiple of 0x10.
                    // However, these 0s are INCLUDED as part of the string table. Thus, they've already been
                    // read (and skipped)

                    // Create a directory right next to the NSP, using the NSP's file name (no extension)
                    DirectoryInfo parentDir = finfo.Directory;
                    DirectoryInfo nspDir = parentDir.CreateSubdirectory(Path.GetFileNameWithoutExtension(finfo.Name));
                    NSP nsp = new NSP(nspDir.FullName);
                    List<string> ncas = new List<string>();

                    // Copy each file in the NSP to a new file.
                    for (int i = 0; i < files.Length; i++)
                    {
                        // NSPs are just groups of files, but switch titles have very specific files in them
                        // So we allow quick reference to these files
                        string filePath = nspDir.FullName + Path.DirectorySeparatorChar + files[i];
                        if (filePath.ToLower().EndsWith(".cnmt.xml"))
                            nsp.CnmtXML = filePath;
                        else if (filePath.ToLower().EndsWith(".programinfo.xml"))
                            nsp.PrograminfoXML = filePath;
                        else if (filePath.ToLower().EndsWith(".legalinfo.xml"))
                            nsp.LegalinfoXML = filePath;
                        else if (filePath.ToLower().EndsWith(".nacp.xml"))
                            nsp.ControlXML = filePath;
                        else if (filePath.ToLower().EndsWith(".cert"))
                            nsp.Certificate = filePath;
                        else if (filePath.ToLower().EndsWith(".tik"))
                            nsp.Ticket = filePath;
                        else if (filePath.ToLower().StartsWith("icon_") || filePath.ToLower().EndsWith(".jpg"))
                            nsp.AddImage(filePath);
                        else if (filePath.ToLower().EndsWith(".nca"))
                        {
                            if (filePath.ToLower().EndsWith(".cnmt.nca"))
                                nsp.CnmtNCA = filePath;
                            ncas.Add(filePath);
                        }
                        else
                        {
                            logger.Warn($"Unknown file type found in NSP, {filePath}");
                            nsp.AddFile(filePath);
                        }
                        
                        using (FileStream fs = FileUtils.OpenWriteStream(filePath))
                        {
                            logger.Info($"Unpacking NSP from file {path}.");
                            await nspReadStream.CopyToAsync(fs, fileSizes[i]).ConfigureAwait(false);
                            logger.Info($"Copied NSP contents to file {filePath}");
                        }
                    }
                    CNMT cnmt = nsp.CNMT = CNMT.FromXml(nsp.CnmtXML);
                    var cnmtNcas = cnmt.ParseContent();
                    foreach (var ncafile in ncas)
                    {
                        string ncaid = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(ncafile));
                        var entry = cnmtNcas[ncaid];
                        nsp.AddNCAByID(entry.Type, ncaid);
                    }
                    return nsp;
                }
            }
        }
        public static NSP FromDirectory(string path)
        {
            DirectoryInfo directory = new DirectoryInfo(path);
            if (directory.Exists)
            {
                NSP nsp = new NSP(path);
                nsp.CnmtXML = directory.EnumerateFiles("*.cnmt.xml").SingleOrDefault().FullName;
                if (nsp.CnmtXML == null)
                    return null;

                CNMT cnmt = nsp.CNMT = CNMT.FromXml(nsp.CnmtXML);
                var cnmtNcas = cnmt.ParseContent();
                foreach (var e in cnmtNcas)
                {
                    string ncaid = e.Key;
                    var entry = e.Value;
                    nsp.AddNCAByID(entry.Type, ncaid);
                }

                foreach (var jpeg in directory.EnumerateFiles("*.jpg"))
                    nsp.AddImage(jpeg.FullName);

                nsp.ControlXML = directory.EnumerateFiles("*.nacp.xml").SingleOrDefault().FullName;
                nsp.LegalinfoXML = directory.EnumerateFiles("*.legalinfo.xml").SingleOrDefault().FullName;
                nsp.PrograminfoXML = directory.EnumerateFiles("*.programinfo.xml").SingleOrDefault().FullName;
                nsp.Certificate = directory.EnumerateFiles("*.cert").SingleOrDefault().FullName;
                nsp.Ticket = directory.EnumerateFiles("*.tik").SingleOrDefault().FullName;
                return nsp;
            }

            return null;
        }

        public void Verify()
        {
            if (CnmtXML == null || NcaFiles == null)
                return;

            CNMT cnmt = CNMT = CNMT.FromXml(CnmtXML);
            var cnmtNcas = cnmt.ParseContent();
            foreach (string ncafile in this.NcaFiles)
            {
                string ncaid = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(ncafile));
                var entry = cnmtNcas[ncaid];

                logger.Info($"Verifying NSP from file {ncafile}.");
                bool good = Crypto.VerifySha256Hash(ncafile, entry.HashData);
                if (!good) throw new BadNcaException(ncafile, "Hash of NCA file didn't match expected hash from CNMT");
                else logger.Info($"Verification succeeded.");
            }
        }

        public async static Task<NSP> Unpack(string nspFile)
        {
            return await ParseNSP(nspFile).ConfigureAwait(false);
        }

        public void AddFile(string filePath)
        {
            this.miscFiles.Add(filePath);
        }

        internal string AddNCAByID(NCAType type, string ncaID)
        {
            if (!NCAs.ContainsKey(type))
                NCAs.Add(type, new List<string>());

            string file = (type == NCAType.Meta) ? ncaID + ".cnmt.nca" : ncaID + ".nca";
            file = this.Directory + Path.DirectorySeparatorChar + file;
            NCAs[type].Add(file);

            return file;
        }

        internal string AddNCAFile(NCAType type, string file)
        {
            if (!NCAs.ContainsKey(type))
                NCAs.Add(type, new List<string>());

            NCAs[type].Add(file);

            return file;
        }

        internal void AddImage(string destFile)
        {
            this.IconFiles.Add(destFile);
        }
    }
}
