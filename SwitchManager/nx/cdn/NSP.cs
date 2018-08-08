using SwitchManager.nx.collection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.cdn
{
    public class NSP
    {
        private Dictionary<NCAType, List<string>> NCAs = new Dictionary<NCAType, List<string>>();
        public List<string> Files
        {
            get
            {
                List<string> files = new List<string>();
                files.Add(Certificate);
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

                return files;
            }
        }
        
        public SwitchTitle Title { get; set; }
        public string Certificate { get; private set; }
        public string Ticket { get; private set; }
        public string CnmtNCA { get; private set; }
        public string CnmtXML { get; private set; }

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
        public void Repack(string path)
        {
            return;
            Console.WriteLine("\tRepacking to NSP...");
            var hd = GenerateHeader();

            string[] files = this.Files.ToArray();
            int nFiles = files.Length;

            // Use lambda to sum sizes of all files in files array
            long totalSize = hd.Length + files.Sum(s => new FileInfo(s).Length);
            
            /*
        
        totSize = len(hd) + sum(os.path.getsize(file) for file in self.files)
        if os.path.exists(self.path) and os.path.getsize(self.path) == totSize:
            print('\t\tRepack %s is already complete!' % self.path)
            return
            
        t = tqdm(total=totSize, unit='B', unit_scale=True, desc=os.path.basename(self.path), leave=False)
        
        t.write('\t\tWriting header...')
        outf = open(self.path, 'wb')
        outf.write(hd)
        t.update(len(hd))
        
        done = 0
        for file in self.files:
            t.write('\t\tAppending %s...' % os.path.basename(file))
            with open(file, 'rb') as inf:
                while True:
                    buf = inf.read(4096)
                    if not buf:
                        break
                    outf.write(buf)
                    t.update(len(buf))
        t.close()
        
        print('\t\tRepacked to %s!' % outf.name)
        outf.close()
        */
        }

        /// <summary>
        /// Generates the PFS0 (NSP) header
        /// See http://switchbrew.org/index.php?title=NCA_Format#PFS0
        /// </summary>
        /// <returns></returns>
        private byte[] GenerateHeader()
        {
            string[] files = this.Files.ToArray();
            int nFiles = files.Length;
            
            // The size of the header is 0x10, plus one 0x18 size entry for each file, plus the size of the string table
            // The string table is all of the file names, separated by nulls
            int stringTableSize = files.Sum((s) => s.Length + 1);
            int headerSize = 0x10 + (nFiles) * 0x18 + stringTableSize;

            int remainder = 0x10 - headerSize % 0x10;
            headerSize += remainder;
            
            // Calculate the file sizes array (size of file for each file)
            var fileSizes = files.Select(f => new FileInfo(f).Length).ToArray();

            // Calculate the file offsets array (offset of file from start for each file)
            var fileOffsets = new long[nFiles];
            for (int i = 0; i < fileOffsets.Length; i++) // fileOffsets = [sum(fileSizes[:n]) for n in range(filesNb)]
                for (int j = 0; j < i; j++)
                    fileOffsets[i] += fileSizes[j];

            // Calculate the filename lengths array (length of file names)
            var fileNamesLengths = files.Select(f => new FileInfo(f).Name.Length + 1).ToArray(); // fileNamesLengths = [len(os.path.basename(file))+1 for file in self.files] # +1 for the \x00
            
            var stringTableOffsets = new long[nFiles];
            for (int i = 0; i < stringTableOffsets.Length; i++) // = [sum(fileNamesLengths[:n]) for n in range(filesNb)]
                for (int j = 0; j < i; j++)
                    stringTableOffsets[i] += files[j].Length+1;

            byte[] header = new byte[headerSize]; 
            int n = 0;

            // 0x0 + 0x4 PFS0 magic number
            header[n++] = unchecked('P' & 0xFF);
            header[n++] = unchecked('F' & 0xFF);
            header[n++] = unchecked('S' & 0xFF);
            header[n++] = unchecked('0' & 0xFF);

            // 0x4 + 0x4 number of files
            byte[] nfBytes = BitConverter.GetBytes(nFiles); nfBytes.CopyTo(header, n); n += nfBytes.Length ;

            // 0x8 + 0x4 size of string table
            byte[] stBytes = BitConverter.GetBytes(stringTableSize+remainder); stBytes.CopyTo(header, n); n += stBytes.Length;
            
            header[n++] = 0x00;
            header[n++] = 0x00;
            header[n++] = 0x00;
            header[n++] = 0x00;

            for (int i = 0; i < nFiles; i++)
            {
                byte[] foBytes = BitConverter.GetBytes(fileOffsets[i]); foBytes.CopyTo(header, n); n += foBytes.Length;
                byte[] fsBytes = BitConverter.GetBytes(fileSizes[i]); fsBytes.CopyTo(header, n); n += fsBytes.Length;
                byte[] stoBytes = BitConverter.GetBytes(stringTableOffsets[i]); stoBytes.CopyTo(header, n); n += stoBytes.Length;
                header[n++] = 0;
                header[n++] = 0;
                header[n++] = 0;
                header[n++] = 0;
            }

            foreach (var str in files)
            {
                byte[] strBytes = Encoding.UTF8.GetBytes(str); strBytes.CopyTo(header, n); n += strBytes.Length;
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
    }
}
