using System;
using System.Linq;
using System.Security.Cryptography;
using System.IO;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Encodings;
using Org.BouncyCastle.Crypto.Engines;
using System.Security.Cryptography.X509Certificates;
using log4net;
using SwitchManager.io;
using System.Threading.Tasks;

namespace SwitchManager.util
{
    public class Crypto
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(Hactool));

        /*
        public static void Pkcs12ToPfx(string certFile, string keyFile, string pfxFile)
        {
            var publicKey = File.ReadAllText(certFile);
            var privateKey = File.ReadAllText(keyFile);

            var certData = GetBytesFromPEM(publicKey, "CERTIFICATE");
            var pKeyData = GetBytesFromPEM(privateKey, "RSA PRIVATE KEY");

            var certificate = new X509Certificate2(certData, string.Empty,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet | X509KeyStorageFlags.Exportable);

            var rsa = DecodeRSAPrivateKey(pKeyData);
            certificate.PrivateKey = rsa;

            var certificateData = certificate.Export(X509ContentType.Pfx, string.Empty);
            File.WriteAllBytes(pfxFile, certificateData);
        }
        */

        public static void PemToPfx(string certFile, string keyFile, string pfxFile)
        {
            PemReader pemReaderCert = new PemReader(File.OpenText(certFile));
            PemReader pemReaderKey = new PemReader(File.OpenText(keyFile));
            
            AsymmetricCipherKeyPair keyPair = null;
            Org.BouncyCastle.X509.X509Certificate cert = null;
            
            object o = pemReaderCert.ReadObject();
            if (o != null && o is Org.BouncyCastle.X509.X509Certificate)
            {
                cert = (Org.BouncyCastle.X509.X509Certificate)o;
            }

            o = pemReaderKey.ReadObject();
            if (o != null && o is AsymmetricCipherKeyPair)
            {
                keyPair = (AsymmetricCipherKeyPair)o;
            }

            PemToPfx(cert, keyPair, pfxFile);
        }

        public static bool PemToPfx(string pemFile, string pfxFile)
        {
            if (string.IsNullOrWhiteSpace(pemFile)) return false;
            if (!File.Exists(pemFile)) return false;

            PemReader pemReader = new PemReader(File.OpenText(pemFile));
            
            AsymmetricCipherKeyPair keyPair = null;
            Org.BouncyCastle.X509.X509Certificate cert = null;
            RSA rsa = null;

            object o;
            while ((o = pemReader.ReadObject()) != null)
            {
                if (o is Org.BouncyCastle.X509.X509Certificate)
                {
                    cert = (Org.BouncyCastle.X509.X509Certificate)o;
                }
                else if (o is AsymmetricCipherKeyPair)
                {
                    keyPair = (AsymmetricCipherKeyPair)o;
                }
                else if (o is RsaPrivateCrtKeyParameters)
                {
                    var k = ((RsaPrivateCrtKeyParameters)o);
                    rsa = DotNetUtilities.ToRSA(k);
                }
            }

            if (cert == null)
                return false;

            if (keyPair != null)
                return PemToPfx(cert, keyPair, pfxFile);
            else if (rsa != null)
                return PemToPfx(cert, rsa, pfxFile);
            else return false;
        }

        public static bool PemToPfx(Org.BouncyCastle.X509.X509Certificate cert, AsymmetricCipherKeyPair keyPair, string pfxFile)
        {
            Pkcs12Store store = new Pkcs12StoreBuilder().Build();

            X509CertificateEntry[] chain = new X509CertificateEntry[1];
            chain[0] = new X509CertificateEntry(cert);
            store.SetKeyEntry("test", new AsymmetricKeyEntry(keyPair.Private), chain);
            FileStream p12file = File.Create(pfxFile);
            store.Save(p12file, null, new SecureRandom());
            p12file.Close();

            return File.Exists(pfxFile);
        }

        public static bool PemToPfx(Org.BouncyCastle.X509.X509Certificate cert, RSA key, string pfxFile)
        {// Convert BouncyCastle X509 Certificate to .NET's X509Certificate
            var netCert = DotNetUtilities.ToX509Certificate(cert);
            var certBytes = netCert.Export(X509ContentType.Pkcs12);

            // Convert X509Certificate to X509Certificate2
            var cert2 = new X509Certificate2(certBytes);

            // Setup RSACryptoServiceProvider with "KeyContainerName" set
            var csp = new CspParameters();
            csp.KeyContainerName = "KeyContainer";

            var rsaPrivate = new RSACryptoServiceProvider(csp);

            // Import private key from BouncyCastle's rsa
            rsaPrivate.ImportParameters(key.ExportParameters(true));

            // Set private key on our X509Certificate2
            cert2.PrivateKey = rsaPrivate;

            // Export Certificate with private key
            File.WriteAllBytes(pfxFile, cert2.Export(X509ContentType.Pkcs12));

            return File.Exists(pfxFile);
        }

        public static System.Security.Cryptography.X509Certificates.X509Certificate LoadCertificate(string path, string password = null)
        {
            if (!FileUtils.FileExists(path)) return null;
            var certificate = password == null ? new System.Security.Cryptography.X509Certificates.X509Certificate2(path) : new System.Security.Cryptography.X509Certificates.X509Certificate2(path, password);
            return certificate;

            //string contents = File.ReadAllText(path); 
            //byte[] bytes = GetBytesFromPEM(contents, "CERTIFICATE");
            //byte[] bytes = GetBytesFromPEM(contents, "RSA PRIVATE KEY");
            //var certificate = new X509Certificate2(bytes);

            //var certificate = X509Certificate.CreateFromSignedFile(path);
            //var certificate = X509Certificate.CreateFromCertFile(path);
        }

        public static byte[] GetBytesFromPEM(string pemString, string section)
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

            return DecodeBase64(pemString.Substring(start, end));
        }

        public static string EncodeBase64(byte[] data)
        {
            return Convert.ToBase64String(data);
        }

        public static byte[] DecodeBase64(string data)
        {
            return Convert.FromBase64String(data);
        }


        public static byte[] GenerateAESKek(byte[] masterKey, byte[] AESUseSrc, byte[] dauthKEK, byte[] dauthSource)
        {
            byte[] decryptedAESKey = DecryptAES(AESUseSrc, masterKey);
            byte[] decryptedAESKEK = DecryptAES(dauthKEK, decryptedAESKey);
            return DecryptAES(dauthSource, decryptedAESKEK);
        }

        public static byte[] DecryptAES(byte[] Data, byte[] Key)
        {
            RijndaelManaged aes = new RijndaelManaged
            {
                Mode = CipherMode.ECB,
                Key = Key,
                Padding = PaddingMode.None
            };
            var decrypt = aes.CreateDecryptor();
            byte[] Out = decrypt.TransformFinalBlock(Data, 0, 16);
            return Out;
        }

        //Code taken from https://stackoverflow.com/questions/29163493/aes-cmac-calculation-c-sharp
        public static byte[] EncryptAES(byte[] key, byte[] iv, byte[] data)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                AesCryptoServiceProvider aes = new AesCryptoServiceProvider
                {
                    Mode = CipherMode.CBC,
                    Padding = PaddingMode.None
                };

                using (CryptoStream cs = new CryptoStream(ms, aes.CreateEncryptor(key, iv), CryptoStreamMode.Write))
                {
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();

                    return ms.ToArray();
                }
            }
        }

        //------- Parses binary ans.1 RSA private key; returns RSACryptoServiceProvider  ---
        public static RSACryptoServiceProvider DecodeRSAPrivateKey(byte[] privkey)
        {
            byte[] MODULUS, E, D, P, Q, DP, DQ, IQ;

            // ---------  Set up stream to decode the asn.1 encoded RSA private key  ------
            MemoryStream mem = new MemoryStream(privkey);
            BinaryReader binr = new BinaryReader(mem);    //wrap Memory Stream with BinaryReader for easy reading
            byte bt = 0;
            ushort twobytes = 0;
            int elems = 0;
            try
            {
                twobytes = binr.ReadUInt16();
                if (twobytes == 0x8130) //data read as little endian order (actual data order for Sequence is 30 81)
                    binr.ReadByte();        //advance 1 byte
                else if (twobytes == 0x8230)
                    binr.ReadInt16();       //advance 2 bytes
                else
                    return null;

                twobytes = binr.ReadUInt16();
                if (twobytes != 0x0102) //version number
                    return null;
                bt = binr.ReadByte();
                if (bt != 0x00)
                    return null;


                //------  all private key components are Integer sequences ----
                elems = GetIntegerSize(binr);
                MODULUS = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                E = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                D = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                P = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                Q = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                DP = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                DQ = binr.ReadBytes(elems);

                elems = GetIntegerSize(binr);
                IQ = binr.ReadBytes(elems);

                // ------- create RSACryptoServiceProvider instance and initialize with public key -----
                RSACryptoServiceProvider RSA = new RSACryptoServiceProvider();
                RSAParameters RSAparams = new RSAParameters
                {
                    Modulus = MODULUS,
                    Exponent = E,
                    D = D,
                    P = P,
                    Q = Q,
                    DP = DP,
                    DQ = DQ,
                    InverseQ = IQ
                };
                RSA.ImportParameters(RSAparams);
                return RSA;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                binr.Close();
            }
        }

        private static int GetIntegerSize(BinaryReader binr)
        {
            byte bt = 0;
            byte lowbyte = 0x00;
            byte highbyte = 0x00;
            int count = 0;
            bt = binr.ReadByte();
            if (bt != 0x02)     //expect integer
                return 0;
            bt = binr.ReadByte();

            if (bt == 0x81)
                count = binr.ReadByte();    // data size in next byte
            else
              if (bt == 0x82)
            {
                highbyte = binr.ReadByte(); // data size in next 2 bytes
                lowbyte = binr.ReadByte();
                byte[] modint = { lowbyte, highbyte, 0x00, 0x00 };
                count = BitConverter.ToInt32(modint, 0);
            }
            else
            {
                count = bt;     // we already have the data size
            }

            while (binr.ReadByte() == 0x00)
            {   //remove high order zeros in data
                count -= 1;
            }
            binr.BaseStream.Seek(-1, SeekOrigin.Current);       //last ReadByte wasn't a removed zero, so back up a byte
            return count;
        }

        /// <summary>
        /// Does a circular shift left (equivalent to a processors ROL instruction), but 
        /// for an entire byte array, rather than a single byte or int
        /// </summary>
        /// <param name="b"></param>
        /// <returns></returns>
        public static byte[] Rol(byte[] b)
        {
            byte[] r = new byte[b.Length];
            byte carry = 0;

            for (int i = b.Length - 1; i >= 0; i--)
            {
                ushort u = (ushort)(b[i] << 1);
                r[i] = (byte)((u & 0xff) + carry);
                carry = (byte)((u & 0xff00) >> 8);
            }

            return r;
        }
        public static byte[] AESCMAC(byte[] key, byte[] data)
        {
            // 1. Calculate a temporary value k_0 = E_k(0). "0" is an empty byte array.
            byte[] L = EncryptAES(key, new byte[16], new byte[16]);

            // 2. If msb(k0) = 0, then k1 = k0 ≪ 1, else k1 = (k0 ≪ 1) ⊕ C; where C is a certain constant that depends only on b.
            byte[] FirstSubkey = Rol(L);
            if ((L[0] & 0x80) == 0x80)
                FirstSubkey[15] ^= 0x87;

            // 3. If msb(k1) = 0, then k2 = k1 ≪ 1, else k2 = (k1 ≪ 1) ⊕ C.
            byte[] SecondSubkey = Rol(FirstSubkey);
            if ((FirstSubkey[0] & 0x80) == 0x80)
                SecondSubkey[15] ^= 0x87;

            // Generate the CMAC tag
            if (((data.Length != 0) && (data.Length % 16 == 0)) == true)
            {
                for (int j = 0; j < FirstSubkey.Length; j++)
                    data[data.Length - 16 + j] ^= FirstSubkey[j];
            }
            else
            {
                byte[] padding = new byte[16 - data.Length % 16];
                padding[0] = 0x80;

                data = data.Concat<byte>(padding.AsEnumerable()).ToArray();

                for (int j = 0; j < SecondSubkey.Length; j++)
                    data[data.Length - 16 + j] ^= SecondSubkey[j];
            }

            byte[] encResult = EncryptAES(key, new byte[16], data);

            byte[] HashValue = new byte[16];
            Array.Copy(encResult, encResult.Length - HashValue.Length, HashValue, 0, HashValue.Length);

            return HashValue;
        }

        public async static Task<byte[]> ComputeHash<T>(Stream stream) where T : HashAlgorithm, new()
        {
            // Init
            var alg = new T();
            long offset = 0;
            byte[] block = new byte[1024 * 1024]; // 1 MB at a time, just for smoother progress

            long len = stream.Length;
            string name = stream is FileStream ? $"Calculating SHA256 of '{(stream as FileStream).Name}'" : $"Calculatig SHA256 of {len} byte data";
            ProgressJob job = new ProgressJob(name, len, 0);

            job.Start();
            while (offset  < len)
            {
                // For each block:
                int howMany = await stream.ReadAsync(block, 0, block.Length).ConfigureAwait(false);

                if (howMany < block.Length)
                {
                    alg.TransformFinalBlock(block, 0, howMany);
                    job.UpdateProgress(howMany);
                    break;
                }
                else
                {
                    offset += alg.TransformBlock(block, 0, howMany, null, 0);
                    job.UpdateProgress(howMany);
                }
            }

            // Get the has code
            byte[] hash = alg.Hash;

            job.Finish();
            alg.Dispose();
            return hash;
        }

        public static byte[] ComputeHash<T>(byte[] buf) where T : HashAlgorithm, new()
        {
            var alg = new T();
            return alg.ComputeHash(buf);
        }

        public static async Task<bool> VerifySha256Hash(string path, byte[] expectedHash)
        {
            using (FileStream fs = File.OpenRead(path))
            {
                byte[] hash = await ComputeHash<SHA256Managed>(fs).ConfigureAwait(false);
                if (expectedHash.Length != hash.Length) // hash has to be 32 bytes = 256 bit
                {
                    logger.Error($"Bad parsed hash file for {path}, not the right length");
                    return false;
                }
                for (int i = 0; i < hash.Length; i++)
                {
                    if (hash[i] != expectedHash[i])
                    {
                        logger.Error($"Hash of downloaded file does not match expected hash!");
                        return false;
                    }
                }
                return true;
            }
        }
    }
}
