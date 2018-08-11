using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SwitchManager.util
{
    public static class Miscellaneous
    {

        public static string ToFileSize(double value)
        {
            string[] suffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
            for (int i = 0; i < suffixes.Length; i++)
            {
                if (value <= (Math.Pow(1024, i + 1)))
                {
                    return ThreeNonZeroDigits(value / Math.Pow(1024, i)) + " " + suffixes[i];
                }
            }

            return ThreeNonZeroDigits(value / Math.Pow(1024, suffixes.Length - 1)) + " " + suffixes[suffixes.Length - 1];
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

        public static byte HexToByte(string byteValue)
        {
            return byte.Parse(byteValue, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture);
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
    }
}
