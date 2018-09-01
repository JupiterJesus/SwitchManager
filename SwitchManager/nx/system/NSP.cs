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
                return this.Files.Sum((s) => s == null ? 0 : Miscellaneous.GetFileSystemSize(s) ?? 0);
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

                if (!string.IsNullOrWhiteSpace(Title.TitleKey))
                    files.Add(Ticket);
                foreach (var type in new[] { NCAType.Program, NCAType.LegalInformation, NCAType.Data, NCAType.HtmlDocument, NCAType.DeltaFragment })
                {
                    if (NCAs.ContainsKey(type))
                        files.AddRange(NCAs[type]);
                }
                files.Add(CnmtNCA);
                files.Add(CnmtXML);

                if (NCAs.ContainsKey(NCAType.Control))
                    files.AddRange(NCAs[NCAType.Control]);

                files.AddRange(this.IconFiles);

                return files;
            }
        }
        
        public SwitchTitle Title { get; set; }
        public string Certificate { get; private set; }
        public string Ticket { get; private set; }
        public string CnmtNCA { get; private set; }
        public string CnmtXML { get; private set; }
        public List<string> IconFiles { get; private set; } = new List<string>();

        public NSP(SwitchTitle title, string certificate, string ticket, string cnmtNca, string cnmtXml)
        {
            this.Title = title;
            this.Certificate = certificate;
            this.Ticket = ticket;
            this.CnmtNCA = cnmtNca;
            this.CnmtXML = cnmtXml;
        }

        /// <summary>
        /// Repacks this NSP from a set of files into a single file and writes the file out to the given path.
        /// TODO: Bugfix and test.
        /// </summary>
        /// <param name="path"></param>
        public async Task<bool> Repack(string path)
        {
            logger.Info($"Repacking to NSP file {path}.");

            string[] files = this.Files.ToArray();
            int nFiles = files.Length;


            var hd = GenerateHeader(files);
            // DEBUGGING NOTES  MY ICONOCLASTS HEADER IS 624 BYTES TOO LONG FIX THIS ASAP

            // Use lambda to sum sizes of all files in files array
            long totalSize = this.FilesSize + hd.Length;

            FileInfo finfo = new FileInfo(path);
            if (finfo.Exists && finfo.Length == totalSize)
            {
                logger.Warn($"NSP already exists {path}. Delete and try again.");
                return true;
            }

            using (JobFileStream str = new JobFileStream(path, "NSP repack of " + Title.Name, totalSize, 0))
            {
                await str.WriteAsync(hd, 0, hd.Length).ConfigureAwait(false);
                // Copy each file to the end of the NSP in sequence. Nothing special here just copy them all.
                foreach (var file in files)
                {
                    using (FileStream fs = File.OpenRead(file))
                        await str.CopyFromAsync(fs);
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
            byte[] nfBytes = BitConverter.GetBytes(nFiles); nfBytes.CopyTo(header, n); n += nfBytes.Length ;

            // 0x8 + 0x4 (plus remainder so it reaches a multple of 0x10) size of string table
            byte[] stBytes = BitConverter.GetBytes(stringTableSize+remainder); stBytes.CopyTo(header, n); n += stBytes.Length;

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
                byte[] strBytes = Encoding.UTF8.GetBytes(Path.GetFileName(str )); strBytes.CopyTo(header, n); n += strBytes.Length;
                header[n++] = 0;
            }
            
            while (remainder-- > 0)
                header[n++] = 0;
            
            return header;
        }

        internal void AddNCA(NCAType type, string path)
        {
            if (!NCAs.ContainsKey(type))
                NCAs.Add(type, new List<string>());

            NCAs[type].Add(path);
        }

        internal void AddImage(string destFile)
        {
            this.IconFiles.Add(destFile);
        }
    }
}
