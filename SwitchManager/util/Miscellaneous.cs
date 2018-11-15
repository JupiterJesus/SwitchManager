using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.util
{
    public static class Miscellaneous
    {

        public static string SanitizeFileName(string str)
        {
            StringBuilder sb = new StringBuilder();
            // Remove bullshit characters before creating path
            
            var invalid = Path.GetInvalidFileNameChars().ToList();
            invalid.Add('™');
            invalid.Add('®');

            foreach (char c in str)
            {
                if (!invalid.Contains(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }

        private static string[] suffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        public static string ToFileSize(double value)
        {
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (value <= (Math.Pow(1024, i + 1)))
                {
                    return ThreeNonZeroDigits(value / Math.Pow(1024, i)) + " " + suffixes[i];
                }
            }

            return ThreeNonZeroDigits(value / Math.Pow(1024, suffixes.Length - 1)) + " " + suffixes[suffixes.Length - 1];
        }

        public static long FromFileSize(string value)
        {
            // Remove leading and trailing spaces.
            value = value.Trim();

            try
            {
                // Find the last non-alphabetic character.
                int ext_start = 0;
                for (int i = value.Length - 1; i >= 0; i--)
                {
                    // Stop if we find something other than a letter.
                    if (!char.IsLetter(value, i))
                    {
                        ext_start = i + 1;
                        break;
                    }
                }

                // Get the numeric part.
                double number = double.Parse(value.Substring(0, ext_start));

                // Get the extension.
                string suffix;
                if (ext_start < value.Length)
                {
                    suffix = value.Substring(ext_start).Trim().ToUpper();
                    if (suffix == "BYTES") suffix = "bytes";
                }
                else
                {
                    suffix = "bytes";
                }

                // Find the extension in the list.
                int suffix_index = -1;
                for (int i = 0; i < suffixes.Length; i++)
                {
                    if (suffixes[i] == suffix)
                    {
                        suffix_index = i;
                        break;
                    }
                }
                if (suffix_index < 0)
                    throw new FormatException(
                        "Unknown file size extension " + suffix + ".");

                // Return the result.
                return (long)Math.Round(number * Math.Pow(1024.0, suffix_index));
            }
            catch (Exception ex)
            {
                throw new FormatException("Invalid file size format", ex);
            }
        }

        private static string ThreeNonZeroDigits(double value)
        {
            if (value >= 100)
            {
                // No digits after the decimal.
                return value.ToString("0,0");
            }
            else if (value >= 10)
            {
                // One digit after the decimal.
                return value.ToString("0.0");
            }
            else
            {
                // Two digits after the decimal.
                return value.ToString("0.00");
            }
        }

        public static string BytesToHex(byte[] v)
        {
            return BitConverter.ToString(v).Replace("-", "").ToLower();
        }

        public static string LongToHex(ulong l)
        {
            return l.ToString("x16");
        }
        
        public static byte HexToByte(string byteValue)
        {
            return byte.Parse(byteValue, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
        }

        public static ulong HexToLong(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return 0;

            if (hex.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                hex = hex.Substring(2);
            }
            return ulong.Parse(hex, NumberStyles.HexNumber);
        }

        /// <summary>
        /// Converts a hex string into bytes and returns a new array with the result.
        /// The number of bytes in the array is half of the length of the hex string. Duh.
        /// </summary>
        /// <param name="hex"></param>
        /// <returns></returns>
        public static byte[] HexToBytes(string hex)
        {
            byte[] bytes = new byte[hex.Length / 2];
            HexToBytes(hex, bytes, 0);

            return bytes;
        }

        /// <summary>
        /// Converts a hex string into bytes, which are copied into an array at the specified position.
        /// The number of bytes copied is half of the length of the hex string, so the hex string should be
        /// even (why would you have an odd-length hex string?).
        /// </summary>
        /// <param name="hex"></param>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        public static void HexToBytes(string hex, byte[] bytes, int offset)
        {
            if (hex.Length % 2 != 0) throw new Exception("Tried to get value of non-even-length hex string");

            // Copy the rights ID in there too at 0x2A0, also 16 bytes (32 characters) long
            int numBytes = hex.Length / 2;
            for (int n = 0; n < numBytes; n++)
            {
                string byteValue = hex.Substring(n * 2, 2);
                bytes[offset + n] = HexToByte(byteValue);
            }
        }

        internal static bool IsHexString(string tkey)
        {
            if (tkey == null) return false;
            tkey = tkey.Trim().ToLower();

            foreach (char c in tkey)
            {
                if (('0' <= c && c <= '9') || ('a' <= c && c <= 'f')) continue;

                return false;
            }
            return true;
        }
    }
}
