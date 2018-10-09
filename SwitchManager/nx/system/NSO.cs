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
    /// <summary>
    /// The switch's executable format, NSO.
    /// Incomplete, also not used for anything.
    /// https://switchbrew.org/wiki/NSO
    /// </summary>
    public class NSO
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(NSO));
        
        public NSO()
        {
        }

        /// <summary>
        /// </summary>
        /// <param name="path"></param>
        public static NSO ParseNSO(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                logger.Error("Empty path passed to ParseNSO.");
                return null;
            }

            FileInfo finfo = new FileInfo(path);
            if (!finfo.Exists)
            {
                logger.Error($"Non-existent file passed to ParseNSO: {path}");
                return null;
            }
            
            using (JobFileStream nspReadStream = new JobFileStream(path, "Parsing NSO at " + path, finfo.Length, 0))
            {
                using (BinaryReader br = new BinaryReader(nspReadStream))
                {
                    if (br.ReadChar() != 'N') throw new InvalidNspException("Wrong header");
                    if (br.ReadChar() != 'S') throw new InvalidNspException("Wrong header");
                    if (br.ReadChar() != 'O') throw new InvalidNspException("Wrong header");
                    if (br.ReadChar() != '0') throw new InvalidNspException("Wrong header");

                    // 0x4 + 0x4 version (0?)
                    int version = br.ReadInt32();

                    // 0x8 + 0x4  reserved/unused
                    int reserved = br.ReadInt32();

                    // 0xC + 0x4 Flags, bit 0-2: (.text, .rodata and .data) section is compressed, bit 3-5: check section hash when loading 
                    uint flags = br.ReadUInt32();

                    // 0x10 + 0xC .text SegmentHeader
                    uint textFileOffset = br.ReadUInt32();
                    uint textMemoryOffset = br.ReadUInt32();
                    uint textDecompressedSize = br.ReadUInt32();

                    // 0x1C + 0x4 Module offset (calculated by sizeof(header)) 
                    int moduleOffset = br.ReadInt32();

                    // 0x20 + 0xC .rodata SegmentHeader
                    uint rodataFileOffset = br.ReadUInt32();
                    uint rodataMemoryOffset = br.ReadUInt32();
                    uint rodataDecompressedSize = br.ReadUInt32();

                    // 0x2C + 0x4 Module file size
                    uint moduleFileSize = br.ReadUInt32();

                    // 0x30 + 0xC .data SegmentHeader
                    uint dataFileOffset = br.ReadUInt32();
                    uint dataMemoryOffset = br.ReadUInt32();
                    uint dataDecompressedSize = br.ReadUInt32();

                    // 0x3C + 0x4 bssSize
                    uint bssSize = br.ReadUInt32();

                    // 0x40 + 0x20 Value of "build id" from ELF's GNU .note section. Contains variable sized digest, up to 32bytes. 
                    byte[] buildId = br.ReadBytes(0x20);

                    // 0x60 + 0x4  	.text compressed size 
                    uint textCompressedSize = br.ReadUInt32();

                    // 0x64 + 0x4  	.rodata compressed size 
                    uint rodataCompressedSize = br.ReadUInt32();

                    // 0x68 + 0x4  	.data compressed size 
                    uint dataCompressedSize = br.ReadUInt32();

                    // 0x6C + 0x1C Reserved (Padding) 
                    br.ReadBytes(0x1C);

                    // 0x88 + 0x8 .rodata - relative extents of .api_info
                    uint apiInfoRegionRoDataOffset = br.ReadUInt32();
                    uint apiInfoRegionSize = br.ReadUInt32();

                    // 0x90 + 0x8 .rodata - relative extents of .dynstr
                    uint dynStrRegionRoDataOffset = br.ReadUInt32();
                    uint dynStrRegionSize = br.ReadUInt32();

                    // 0x98 + 0x8 .rodata - relative extents of .dynsym
                    uint dynSymRegionRoDataOffset = br.ReadUInt32();
                    uint dynSymRegionSize = br.ReadUInt32();

                    // 0xA0 + 0x20 * 3 SHA256 hashes over the decompressed sections using the above byte-sizes: .text, .rodata, and .data. 
                    byte[] textHash = br.ReadBytes(0x20);
                    byte[] rodataHash = br.ReadBytes(0x20);
                    byte[] dataHash = br.ReadBytes(0x20);

                    // 0x100 compressed sections

                    return new NSO();
                }
            }
        }
    }
}
