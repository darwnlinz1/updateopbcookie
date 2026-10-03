using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;

namespace RuriLib.Functions.Formats
{
    public static class ProtobufWireDecoder
    {
        public static string DecodeToJson(byte[] data)
        {
            if (data == null || data.Length == 0) return "{}";
            try
            {
                var jobj = DecodeMessage(data, 0, data.Length, 0);
                return jobj.ToString(Newtonsoft.Json.Formatting.None);
            }
            catch (Exception ex)
            {
                return string.Format("{{\"error\":\"Failed to decode protobuf: {0}\"}}", ex.Message);
            }
        }

        private static JObject DecodeMessage(byte[] data, int offset, int length, int depth)
        {
            if (depth > 20) throw new InvalidOperationException("Max protobuf recursion depth exceeded");
            var result = new JObject();
            int end = offset + length;
            int cursor = offset;

            while (cursor < end)
            {
                ulong tagKey;
                if (!TryReadVarint(data, ref cursor, end, out tagKey))
                    break;

                int fieldNum = (int)(tagKey >> 3);
                int wireType = (int)(tagKey & 7);
                string key = fieldNum.ToString();

                JToken val = null;

                switch (wireType)
                {
                    case 0: // Varint
                        ulong varintVal;
                        if (TryReadVarint(data, ref cursor, end, out varintVal))
                        {
                            val = new JValue(varintVal);
                        }
                        break;

                    case 1: // 64-bit
                        if (cursor + 8 <= end)
                        {
                            ulong f64 = BitConverter.ToUInt64(data, cursor);
                            cursor += 8;
                            val = new JValue(f64);
                        }
                        else
                        {
                            cursor = end;
                        }
                        break;

                    case 2: // Length-delimited
                        ulong lenVal;
                        if (TryReadVarint(data, ref cursor, end, out lenVal))
                        {
                            int len = (int)lenVal;
                            if (cursor + len <= end)
                            {
                                int subOffset = cursor;
                                cursor += len;

                                JObject subObj;
                                string strVal;

                                // Try decoding as sub-message
                                if (len > 0 && TryDecodeSubMessage(data, subOffset, len, depth + 1, out subObj))
                                {
                                    val = subObj;
                                }
                                // Try decoding as UTF-8 string
                                else if (TryDecodeUtf8String(data, subOffset, len, out strVal))
                                {
                                    val = new JValue(strVal);
                                }
                                else
                                {
                                    // Fallback to Base64
                                    byte[] rawBytes = new byte[len];
                                    Buffer.BlockCopy(data, subOffset, rawBytes, 0, len);
                                    val = new JValue(Convert.ToBase64String(rawBytes));
                                }
                            }
                            else
                            {
                                cursor = end;
                            }
                        }
                        else
                        {
                            cursor = end;
                        }
                        break;

                    case 5: // 32-bit
                        if (cursor + 4 <= end)
                        {
                            uint f32 = BitConverter.ToUInt32(data, cursor);
                            cursor += 4;
                            val = new JValue(f32);
                        }
                        else
                        {
                            cursor = end;
                        }
                        break;

                    default:
                        cursor = end;
                        break;
                }

                if (val != null)
                {
                    if (result.ContainsKey(key))
                    {
                        var existing = result[key];
                        var arr = existing as JArray;
                        if (arr != null)
                        {
                            arr.Add(val);
                        }
                        else
                        {
                            var newArr = new JArray { existing, val };
                            result[key] = newArr;
                        }
                    }
                    else
                    {
                        result[key] = val;
                    }
                }
            }

            return result;
        }

        private static bool TryDecodeSubMessage(byte[] data, int offset, int length, int depth, out JObject subObj)
        {
            subObj = null;
            if (length <= 1) return false;
            try
            {
                int tempCursor = offset;
                ulong firstKey;
                if (!TryReadVarint(data, ref tempCursor, offset + length, out firstKey))
                    return false;
                int fieldNum = (int)(firstKey >> 3);
                int wireType = (int)(firstKey & 7);
                if (fieldNum == 0 || wireType > 5 || wireType == 3 || wireType == 4)
                    return false;

                var testObj = DecodeMessage(data, offset, length, depth);
                if (testObj.Count > 0)
                {
                    subObj = testObj;
                    return true;
                }
            }
            catch { }
            return false;
        }

        private static bool TryDecodeUtf8String(byte[] data, int offset, int length, out string result)
        {
            result = null;
            if (length == 0) { result = string.Empty; return true; }

            // Check printable characters
            bool hasNonPrintable = false;
            for (int i = 0; i < length; i++)
            {
                byte b = data[offset + i];
                if (b < 0x20 && b != 0x09 && b != 0x0A && b != 0x0D)
                {
                    hasNonPrintable = true;
                    break;
                }
            }

            if (hasNonPrintable) return false;

            try
            {
                result = Encoding.UTF8.GetString(data, offset, length);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryReadVarint(byte[] data, ref int cursor, int end, out ulong value)
        {
            value = 0;
            int shift = 0;
            while (cursor < end && shift < 64)
            {
                byte b = data[cursor++];
                value |= ((ulong)(b & 0x7F)) << shift;
                if ((b & 0x80) == 0) return true;
                shift += 7;
            }
            return false;
        }
    }
}
