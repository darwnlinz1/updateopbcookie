using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace RuriLib.Functions.Requests
{
    public static class HttpCompression
    {
        public static (string Body, byte[] RawBytes) Decompress(byte[] rawBytes, string contentEncoding, string contentType = null)
        {
            if (rawBytes == null || rawBytes.Length == 0)
            {
                return (string.Empty, rawBytes);
            }

            if (string.IsNullOrWhiteSpace(contentEncoding))
            {
                var enc = GetEncoding(contentType);
                return (enc.GetString(rawBytes), rawBytes);
            }

            string encLower = contentEncoding.Trim().ToLowerInvariant();
            byte[] decompressedBytes = null;

            try
            {
                if (encLower.Contains("br"))
                {
                    decompressedBytes = BrotliSharpLib.Brotli.DecompressBuffer(rawBytes, 0, rawBytes.Length);
                }
                else if (encLower.Contains("gzip"))
                {
                    using (var inMs = new MemoryStream(rawBytes))
                    using (var gz = new GZipStream(inMs, CompressionMode.Decompress))
                    using (var outMs = new MemoryStream())
                    {
                        gz.CopyTo(outMs);
                        decompressedBytes = outMs.ToArray();
                    }
                }
                else if (encLower.Contains("zstd"))
                {
                    using (var inMs = new MemoryStream(rawBytes))
                    using (var zs = new ZstdSharp.DecompressionStream(inMs))
                    using (var outMs = new MemoryStream())
                    {
                        zs.CopyTo(outMs);
                        decompressedBytes = outMs.ToArray();
                    }
                }
                else if (encLower.Contains("deflate"))
                {
                    int offset = 0;
                    // Detect and bypass RFC 1950 2-byte zlib wrapper (0x78)
                    if (rawBytes.Length > 2 && rawBytes[0] == 0x78 &&
                        (rawBytes[1] == 0x01 || rawBytes[1] == 0x9C || rawBytes[1] == 0xDA || rawBytes[1] == 0x5E))
                    {
                        offset = 2;
                    }

                    using (var inMs = new MemoryStream(rawBytes, offset, rawBytes.Length - offset))
                    using (var ds = new DeflateStream(inMs, CompressionMode.Decompress))
                    using (var outMs = new MemoryStream())
                    {
                        ds.CopyTo(outMs);
                        decompressedBytes = outMs.ToArray();
                    }
                }
            }
            catch
            {
                // Fallback to original bytes on decompression exception
                decompressedBytes = null;
            }

            var finalBytes = decompressedBytes ?? rawBytes;
            var textEncoding = GetEncoding(contentType);
            string finalBody = textEncoding.GetString(finalBytes);

            return (finalBody, finalBytes);
        }

        public static (string Body, byte[] RawBytes) DecompressString(string content, string contentEncoding, string contentType = null)
        {
            if (string.IsNullOrEmpty(content))
            {
                return (string.Empty, new byte[0]);
            }

            byte[] rawBytes = null;
            try
            {
                rawBytes = Convert.FromBase64String(content);
            }
            catch
            {
                rawBytes = Encoding.UTF8.GetBytes(content);
            }

            return Decompress(rawBytes, contentEncoding, contentType);
        }

        private static Encoding GetEncoding(string contentType)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return Encoding.UTF8;
            }

            try
            {
                var match = Regex.Match(contentType, @"charset=([a-zA-Z0-9_\-]+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    string charset = match.Groups[1].Value.Trim('\'', '"', ' ');
                    return Encoding.GetEncoding(charset);
                }
            }
            catch { }

            return Encoding.UTF8;
        }
    }
}
