using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuriLib.Functions.Conversions
{

    /// <summary>
    /// The available conversion formats.
    /// </summary>
    public enum Encoding
    {
        /// <summary>A hexadecimal representation of a byte array.</summary>
        HEX,

        /// <summary>A binary representation of a byte array, containing a multiple of 8 binary digits.</summary>
        BIN,

        /// <summary>A base64 representation of a byte array.</summary>
        BASE64,

        /// <summary>An ASCII string representation of a byte array.</summary>
        ASCII,

        /// <summary>A UTF8 string representation of a byte array.</summary>
        UTF8,

        /// <summary>A UTF16 Unicode string representation of a byte array.</summary>
        UNICODE
    }

    /// <summary>
    /// Provides methods to convert between different representations of binary data.
    /// </summary>
    public static class Conversion
    {
        private static readonly char[] HexLookupUpper = new char[]
        {
            '0', '1', '2', '3', '4', '5', '6', '7',
            '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'
        };

        /// <summary>
        /// Converts an encoded input to a byte array.
        /// </summary>
        /// <param name="input">The encoded input</param>
        /// <param name="encoding">The encoding</param>
        /// <returns>The converted byte array</returns>
        public static byte[] ConvertFrom(this string input, Encoding encoding)
        {
            switch (encoding)
            {
                case Encoding.BASE64:
                    return Convert.FromBase64String(input);

                case Encoding.HEX:
                    if (string.IsNullOrEmpty(input)) return new byte[0];
                    string hexClean = input.Replace("0x", "").Replace(" ", "").Replace("\t", "").Replace("\r", "").Replace("\n", "");
                    if (hexClean.Length % 2 != 0) hexClean = "0" + hexClean;
                    byte[] hexBytes = new byte[hexClean.Length / 2];
                    for (int i = 0; i < hexBytes.Length; i++)
                    {
                        int hi = GetHexVal(hexClean[i * 2]);
                        int lo = GetHexVal(hexClean[i * 2 + 1]);
                        hexBytes[i] = (byte)((hi << 4) | lo);
                    }
                    return hexBytes;

                case Encoding.BIN:
                    int numOfBytes = input.Length / 8;
                    byte[] bytes = new byte[numOfBytes];
                    for (int i = 0; i < numOfBytes; ++i) { bytes[i] = Convert.ToByte(input.Substring(8 * i, 8), 2); }
                    return bytes;

                case Encoding.ASCII:
                    return System.Text.Encoding.ASCII.GetBytes(input);

                case Encoding.UTF8:
                    return System.Text.Encoding.UTF8.GetBytes(input);

                case Encoding.UNICODE:
                    return System.Text.Encoding.Unicode.GetBytes(input);

                default:
                    return new byte[0];
            }
        }

        private static int GetHexVal(char hex)
        {
            int val = (int)hex;
            // For uppercase A-F: 65 - 70 -> 10 - 15
            // For lowercase a-f: 97 - 102 -> 10 - 15
            // For digits 0-9: 48 - 57 -> 0 - 9
            return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }

        /// <summary>
        /// Converts a byte array to an encoded string.
        /// </summary>
        /// <param name="input">The byte array to encode</param>
        /// <param name="encoding">The encoding</param>
        /// <returns>The encoded string</returns>
        public static string ConvertTo(this byte[] input, Encoding encoding)
        {
            if (input == null || input.Length == 0) return string.Empty;
            switch (encoding)
            {
                case Encoding.BASE64:
                    return Convert.ToBase64String(input);

                case Encoding.HEX:
                    char[] c = new char[input.Length * 2];
                    for (int i = 0; i < input.Length; i++)
                    {
                        byte b = input[i];
                        c[i * 2] = HexLookupUpper[b >> 4];
                        c[i * 2 + 1] = HexLookupUpper[b & 0xF];
                    }
                    return new string(c);

                case Encoding.BIN:
                    return string.Concat(input.Select(b => Convert.ToString(b, 2).PadLeft(8, '0')));

                case Encoding.ASCII:
                    return System.Text.Encoding.ASCII.GetString(input);

                case Encoding.UTF8:
                    return System.Text.Encoding.UTF8.GetString(input);

                case Encoding.UNICODE:
                    return System.Text.Encoding.Unicode.GetString(input);

                default:
                    return "";
            }
        }
    }
}
