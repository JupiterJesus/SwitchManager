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
        private string path;
        private string[] files;

        public NSP(string path, string[] files)
        {
            this.path = path;
            this.files = files;
        }

        public void Repack()
        {
            Console.WriteLine("\tRepacking to NSP...");
            var hd = GenerateHeader();

            // Use lambda to sum sizes of all files in files array
            long totalSize = hd.Length + this.files.Sum(s => new FileInfo(s).Length);
            
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

        private byte[] GenerateHeader()
        {
            int filesNb = this.files.Length;
            /*
            stringTable = '\x00'.join(os.path.basename(file) for file in self.files)
            */

            // TODO Figure out exactly what python join does and what the above means
            char[] stringTable = new char[0];

            int headerSize = 0x10 + (filesNb) * 0x18 + stringTable.Length;
            int remainder = 0x10 - headerSize % 0x10;
            headerSize += remainder;

            var fileSizes = files.Select(f => new FileInfo(f).Length).ToArray();
            var fileOffsets = new long[files.Length];
            for (int i = 0; i < fileOffsets.Length; i++) // fileOffsets = [sum(fileSizes[:n]) for n in range(filesNb)]
                for (int j = 0; j < i; j++)
                    fileOffsets[i] += fileSizes[j];

            var fileNamesLengths = files.Select(f => new FileInfo(f).Name.Length + 1).ToArray(); // fileNamesLengths = [len(os.path.basename(file))+1 for file in self.files] # +1 for the \x00
            /*
            
            
            stringTableOffsets = [sum(fileNamesLengths[:n]) for n in range(filesNb)]
            */

            // TODO the above files sizes, offsets, etc might be better calculated below in the place where they are copied into the header
            byte[] header =  new byte[0x1000]; // Making up a size...
            uint n = 0;
            header[n++] = unchecked('P' & 0xFF);
            header[n++] = unchecked('F' & 0xFF);
            header[n++] = unchecked('S' & 0xFF);
            header[n++] = unchecked('0' & 0xFF);

            /*
            header += pk('<I', filesNb)
            header += pk('<I', len(stringTable)+remainder)
            */

            // TODO figure out what pk is in python and replicate
            header[n++] = 0x00;
            header[n++] = 0x00;
            header[n++] = 0x00;
            header[n++] = 0x00;

            for (int i = 0; i < filesNb; i++)
            {
                /*
                for i in range(filesNb):
                    header += pk('<Q', fileOffsets[i])
                    header += pk('<Q', fileSizes[i])
                    header += pk('<I', stringTableOffsets[i])
                    */

                header[n++] = 0x00;
                header[n++] = 0x00;
                header[n++] = 0x00;
                header[n++] = 0x00;
            }

            /*
            header += stringTable.encode()
            */

            // TODO figure out what pk is in python and replicate
            while (remainder-- > 0)
                header[n++] = 0x00;

            // Put the header into a perfectly sized array so the calling function knows how long it actually is
            byte[] result = new byte[n];
            header.CopyTo(result, 0);
            return result;
        }
    }
}
