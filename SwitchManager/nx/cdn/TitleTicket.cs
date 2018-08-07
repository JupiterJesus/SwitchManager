using System;
using System.IO;

namespace SwitchManager.nx.cdn
{
    /// <summary>
    /// Represents a title ticket. See http://switchbrew.org/index.php?title=Ticket.
    /// TODO: Not currently used. The main code does this all manually without a proper data structure, just a byte array.
    /// </summary>
    internal class TitleTicket
    {
        public byte[] Signature { get; set; }
        public uint SignatureType { get; set; }
        public uint SignatureSize { get; set; }
        public uint SignaturePadding { get; set; }
        public byte[] Issuer { get; private set; }
        public byte[] TitleKey { get; private set; }
        public byte TitleKeyType { get; set; }
        public byte KeyGeneration { get; internal set; }
        public byte MasterKeyRevision { get; set; }
        public byte[] TicketID { get; private set; }
        public byte[] DeviceID { get; private set; }
        public byte[] RightsID { get; private set; }
        public byte[] AccountID { get; private set; }

        public static TitleTicket Load(string filename)
        {
            // Read all of the generic ticket file into a byte array
            byte[] data = File.ReadAllBytes(filename);

            return new TitleTicket(data);
        }

        public TitleTicket(byte[] data)
        {
            SignatureType = BitConverter.ToUInt32(data,0);
            switch (SignatureType)
            {
                case 0x010000: SignatureSize = 0x200; SignaturePadding = 0x3C; break; // RSA_4096 SHA1
                case 0x010001: SignatureSize = 0x100; SignaturePadding = 0x3C; break; // RSA_2048 SHA1
                case 0x010002: SignatureSize = 0x3C; SignaturePadding = 0x40; break; // ECDSA SHA1
                case 0x010003: SignatureSize = 0x200; SignaturePadding = 0x3C; break; // RSA_4096 SHA256
                case 0x010004: SignatureSize = 0x100; SignaturePadding = 0x3C; break; // RSA_2048 SHA256
                case 0x010005: SignatureSize = 0x3C; SignaturePadding = 0x40; break; // ECDSA SHA256
            }

            uint sigLength = 4 + SignatureSize + SignaturePadding;
            Signature = new byte[sigLength]; data.CopyTo(Signature, 0); // I don't know what the first 0x140 bytes are, they aren't documented on switchbrew
            Issuer = new byte[0x40]; data.CopyTo(Issuer, 0 + sigLength);
            TitleKey = new byte[0x100]; data.CopyTo(TitleKey, 0x40 + sigLength);
            TitleKeyType = data[0x141 + sigLength];
            MasterKeyRevision = data[0x145 + sigLength];
            // switchbrew says this should be at 0x285, CDNSP has it at 0x286...
            // Who's right? Does it even matter?
            TicketID = new byte[0x8]; data.CopyTo(TicketID, 0x150 + sigLength);
            DeviceID = new byte[0x8]; data.CopyTo(DeviceID, 0x158 + sigLength);
            RightsID = new byte[0x10]; data.CopyTo(RightsID, 0x160 + sigLength);
            AccountID = new byte[0x4]; data.CopyTo(RightsID, 0x170 + sigLength);
        }

        internal void TitleKeyFromString(string titleKey)
        {
            // This is a bit confusing but I was looking in the ticket file and I found that the issuer,
            // which is 64 bytes starting at 0x0, is actually 320 bytes in at 0x140.
            // The offsets we have below are 0x180, 0x286 and 0x2A0
            // If we subtract 0x140, we get 0x40, 0x146 and 0x160
            // According to http://switchbrew.org/index.php?title=Ticket,
            // 0x40 has the title key block, 0x146 is 10 "unknown" bytes and 0x160 is the rights ID
            // The doc doesn't mention anything about the KeyGeneration value from the header
            // Copy the 16-byte value of the 32 character hex title key into memory starting at position 0x180
            // SO, what is in that first 320 bytes of this file?
            for (int n = 0; n < 0x10; n++)
            {
                string byteValue = titleKey.Substring(n * 2, 2);
                TitleKey[n] = HexToByte(byteValue);
            }
        }

        private static byte HexToByte(string byteValue)
        {
            return byte.Parse(byteValue, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        }

        internal void RightsIdFromString(string rightsID)
        { 
            // Copy the rights ID in there too at 0x2A0, also 16 bytes (32 characters) long
            for (int n = 0; n < 0x10; n++)
            {
                string byteValue = rightsID.Substring(n * 2, 2);
                RightsID[n] = byte.Parse(byteValue, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        internal void SaveTo(string ticketPath)
        {
            using (FileStream fs = File.Create(ticketPath))
            {
                fs.Write(Signature, 0, Signature.Length); // Signature data, variable length
                fs.Write(Issuer, 0, Issuer.Length);  // 0x0 + 0x40
                fs.Write(TitleKey, 0, TitleKey.Length); // 0x40 + 0x100
                fs.WriteByte(0); // 0x140 + 0x1 unknown byte
                fs.WriteByte(TitleKeyType); // 0x141 + 0x1 
                for (int i = 0; i < 4; i++) fs.WriteByte(0); // 4 unknown bytes from 0x142 through 0x145
                fs.WriteByte(MasterKeyRevision); // 0x146 + 0x1
                for (int i = 0; i < 9; i++) fs.WriteByte(0); // 9 unknown bytes from 0x147 through 0x14F
                fs.Write(TicketID, 0, TicketID.Length); // 0x150 + 0x8
                fs.Write(DeviceID, 0, DeviceID.Length); // 0x158 + 0x8
                fs.Write(RightsID, 0, RightsID.Length); // 0x160 + 0x10
                fs.Write(AccountID, 0, AccountID.Length); // 0x170 + 0x4
                //for (int i = 0; i < 0xC + 0x140; i++) fs.WriteByte(0); // unknown crap at the end ?

            }
        }
    }
}