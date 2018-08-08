using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.nx.cdn
{
    /// <summary>
    /// Represents a CNMT file, which is a type of metadata file for NSPs (I think).
    /// See http://switchbrew.org/index.php?title=NCA
    /// </summary>
    public class CNMT
    {
        public string CnmtFilePath { get; set; }
        public string CnmtDirectory { get; set; }
        public string CnmtNcaFile { get; set; }

        public string ID { get; set; }
        public uint Version { get; set; }
        public TitleType Type { get; set; }

        private ushort tableOffset;
        private ushort numContentEntries;
        private ushort numMetaEntries;
        private string requiredDownloadSysVersion;
        private string minSysVersion;
        private byte[] hash;
        public byte MasterKeyRevision { get; set; }

        public CNMT(string filePath, string headerPath, string cnmtDir, string ncaPath)
        {
            this.CnmtFilePath = filePath;
            this.CnmtDirectory = cnmtDir;
            this.CnmtNcaFile = ncaPath;

            FileStream fs = File.OpenRead(filePath);
            BinaryReader br = new BinaryReader(fs);

            // Reading CNMT file
            // See http://switchbrew.org/index.php?title=NCA#Metadata_file

            // Title ID is at offset 0 and is 8 bytes long
            this.ID = $"{br.ReadUInt64():X16}";

            // Title Version is immediately after at offset 8 and is 4 bytes long
            this.Version = br.ReadUInt32();

            // Title Type is immediately after at offset 0xC and is 1 byte long
            // See http://switchbrew.org/index.php?title=NCM_services#Title_Types
            this.Type = (TitleType)br.ReadByte();

            // What's at 0xD?
            br.ReadByte();

            // Offset to table relative to the end of this 0x20-byte header, whatever that means
            this.tableOffset = br.ReadUInt16();

            // Number of content entries, offset 0x10, 2 bytes long
            this.numContentEntries = br.ReadUInt16();

            // Number of meta entries, offset 0x12, 2 bytes long
            this.numMetaEntries = br.ReadUInt16();

            // I see this in CDNSP but I don't see it in the doc
            // This is the 8 bytes before the app header starts at 0x20
            // The docs give no information about the 12 bytes starting at 0x14 (up to 0x20)
            br.BaseStream.Seek(0x18, SeekOrigin.Begin); this.requiredDownloadSysVersion = br.ReadUInt64().ToString();

            // Minimum System Version is at offset 0x28 and is 8 bytes long
            br.BaseStream.Seek(0x28, SeekOrigin.Begin); this.minSysVersion = br.ReadUInt64().ToString();

            // Get the hash/digest from the last 0x20 bytes
            br.BaseStream.Seek(-0x20, SeekOrigin.End); this.hash = br.ReadBytes(0x20);
            
            br.Close();

            fs = File.OpenRead(headerPath);
            br = new BinaryReader(fs);
            // Grabs the first 0x220 bytes from Header.bin and stores them for later
            // See http://switchbrew.org/index.php?title=NCA_Format#Header
            // This is used later in the rights ID
            br.BaseStream.Seek(0x220, SeekOrigin.Begin);
            this.MasterKeyRevision = br.ReadByte();
            br.Close();
        }

        public Dictionary<string, CnmtMetaEntry> ParseSystemUpdate()
        {
            // System updates are different and get their own parser
            // Don't try to call parsesystemupdate if it isn't one!
            // This was the easiest solution to coding a file with drastically different file formats
            // in a strongly typed language.
            // I also considered storing everything in a dictionary of strings, or in a generic json container
            // but I ultimately settled on having one parse for system update that handles system updates and
            // one that handles other stuff
            // I might even have two different CNMTs with different parse methods, subclassing CNMT, I dunno
            if (this.Type != TitleType.SystemUpdate)
                throw new Exception("Do not call ParseSystemUpdate unless the CNMT is for a System Update title!!!");

            var data = new Dictionary<string, CnmtMetaEntry>();
            FileStream fs = File.OpenRead(CnmtFilePath);
            BinaryReader br = new BinaryReader(fs);

            // Reach each entry, starting at 0x20 (after the header ends)
            // each entry is 0x10 in size, and their are numMetaEntries (stored at 0x12) of them
            // With patch, update and addon types, there's a secondary header from 0x20 to 0x30
            // 0xE tells you how many bytes to skip from 0x20 to the start of the content entries and 0x10 says how many there are
            // With System Update, 0xE is blank, but 0x12 tells you how many metadata entries there are, starting at 0x20
            // See http://switchbrew.org/index.php?title=NCA
            br.BaseStream.Seek(0x20, SeekOrigin.Begin);
            for (int i = 0; i < this.numMetaEntries; i++)
            {
                var meta = new CnmtMetaEntry();
                // Parse a meta entry
                meta.TitleID = $"{br.ReadUInt64():X16}"; // Title ID, offset 0x0, 8 bytes, we store it in hex to match other title ids
                meta.Version = br.ReadUInt32(); // Version, offset 0x8, 4 bytes, store it as uint to match other versions, even though in hex it is always in the format 0xN0000, where N is the version number OMG why don't I fucking store this shit in hex? oh well whatever
                meta.Type = (TitleType)br.ReadByte(); // Title Type, offset 0xC, 1 byte
                meta.Flag = br.ReadByte(); // Unknown flag bit?, not using, offset 0xD, 1 byte
                meta.Unknown = br.ReadUInt16(); // Unused? Skip, offset 0xE, 2 bytes
                // restart loop 0x10 higher than the last loop read to read next meta entry
                data[meta.TitleID] = meta;
            }
            return data;
        }


        public Dictionary<string, CnmtContentEntry> Parse(NCAType? ncaType = null)
        {
            // Standard Parse is for everything other than system updates
            // This might be more elegant with subclasses or an interface or something,
            // but I would still come right back to having to have a single data type returned by the
            // virtual function that encompassed both content and metadata entries, and I just don't want to
            // They are completely different and shouldn't be combined into one class
            if (this.Type == TitleType.SystemUpdate)
                throw new Exception("Do not call Parse on a System Update title!!! Only for other types!");

            var data = new Dictionary<string, CnmtContentEntry>();
            FileStream fs = File.OpenRead(this.CnmtFilePath);
            BinaryReader br = new BinaryReader(fs);

            // Reach each content entry, starting at 0x20 + tableOffset
            // each entry is 0x38 in size, and their are nEntries of them
            // With patch, update and addon types, there's a secondary header from 0x20 to 0x30
            // 0xE tells you how many bytes to skip from 0x20 to the start of the content entries and 0x10 says how many there are
            br.BaseStream.Seek(0x20 + this.tableOffset, SeekOrigin.Begin);
            for (int i = 0; i < this.numContentEntries; i++)
            {
                // Parse a content entry
                var content = new CnmtContentEntry();
                content.Hash = br.ReadBytes(0x20); // Hash, offset 0x0, 32 bytes
                content.NcaId = BitConverter.ToString(br.ReadBytes(0x10)).Replace("-",""); // NCA ID, offset 0x20, 16 bytes, convert bytes to a hex string
                byte[] sizeBuffer = new byte[8];
                br.Read(sizeBuffer, 0, 6);
                content.EntrySize = BitConverter.ToUInt64(sizeBuffer, 0); // Size, offset 0x30, 6 bytes (8 byte long converted from only 6 bytes)
                content.NcaType = (NCAType)br.ReadByte(); // Type (0=meta, 1=program, 2=data, 3=control, 4=offline-manual html, 5=legal html, 6=game-update RomFS patches?), offset 0x36, 1 byte
                content.Unknown = br.ReadByte(); // Unknown, offset 0x37, 1 byte

                // Only keep entries of the type we care about, or keep all of them if no type was specified
                if (content.NcaType == ncaType || !ncaType.HasValue)
                {
                    data[content.NcaId] = content;
                }
                // restart loop 0x38 higher than the last loop read to read next meta entry
            }
            br.Close();
            return data;
        }

        public string GenerateXml(string outFile)
        {
            if (this.Type == TitleType.SystemUpdate)
            {
                var data = this.ParseSystemUpdate();
            }
            else
            {
                var data = this.Parse();
            }

            File.Create(outFile).Close();
            //string headerPath = Path.GetDirectoryName(ncaPath) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(ncaPath) +  Path.DirectorySeparatorChar + "Header.bin";
            /*
             * 
        ContentMeta = ET.Element('ContentMeta')

        ET.SubElement(ContentMeta, 'Type').text = self.type
        ET.SubElement(ContentMeta, 'Id').text = '0x%s' % self.id
        ET.SubElement(ContentMeta, 'Version').text = self.ver
        ET.SubElement(ContentMeta, 'RequiredDownloadSystemVersion').text =3 self.dlsysver

        n = 1
        for tid in data:
            locals()["Content" + str(n)] = ET.SubElement(ContentMeta, 'Content')
            ET.SubElement(locals()["Content" + str(n)], 'Type').text = data[tid][0]
            ET.SubElement(locals()["Content" + str(n)], 'Id').text = tid
            ET.SubElement(locals()["Content" + str(n)], 'Size').text = data[tid][1]
            ET.SubElement(locals()["Content" + str(n)], 'Hash').text = data[tid][2]
            ET.SubElement(locals()["Content" + str(n)], 'KeyGeneration').text = keyGeneration
            n += 1

        # cnmt.nca itself
        cnmt = ET.SubElement(ContentMeta, 'Content')
        ET.SubElement(cnmt, 'Type').text = 'Meta'
        ET.SubElement(cnmt, 'Id').text = os.path.basename(ncaPath).split('.')[0]
        ET.SubElement(cnmt, 'Size').text = str(os.path.getsize(ncaPath))
        hash = sha256()
        with open(ncaPath, 'rb') as nca:
            hash.update(nca.read())  # Buffer not needed
        ET.SubElement(cnmt, 'Hash').text = hash.hexdigest()
        ET.SubElement(cnmt, 'KeyGeneration').text = mKeyRev

        ET.SubElement(ContentMeta, 'Digest').text = self.digest
        ET.SubElement(ContentMeta, 'KeyGenerationMin').text = self.mkeyrev
        global sysver0
        ET.SubElement(ContentMeta, 'RequiredSystemVersion').text = ('0' if sysver0 else self.sysver)
        if self.id.endswith('800'):
            ET.SubElement(ContentMeta, 'PatchId').text = '0x%s000' % self.id[:-3]
        else:
            ET.SubElement(ContentMeta, 'PatchId').text = '0x%s800' % self.id[:-3]

        string = ET.tostring(ContentMeta, encoding='utf-8')
        reparsed = minidom.parseString(string)
        with open(outf, 'w') as f:
            f.write(reparsed.toprettyxml(encoding='utf-8', indent='  ').decode()[:-1])

        */
            Console.WriteLine("Generated XML file {0}!", Path.GetFileName(outFile));
            return (outFile);
        }
    }
}
