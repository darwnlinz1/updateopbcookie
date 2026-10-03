using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Newtonsoft.Json;

namespace RuriLib.Functions.Requests.TlsClient
{
    public static class TlsClientNative
    {
        private const string DllName = "tls-client-windows-64.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr request(IntPtr requestPayload);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr freeMemory(IntPtr memoryId);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr destroySession(IntPtr destroySessionPayload);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr destroyAll();

        [DllImport("msvcrt.dll", CallingConvention = CallingConvention.Cdecl, EntryPoint = "strlen")]
        [SuppressUnmanagedCodeSecurity]
        private static extern UIntPtr strlen(IntPtr str);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool SetDllDirectory(string lpPathName);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        private static bool _initialized = false;
        private static IntPtr _dllHandle = IntPtr.Zero;
        private static readonly object _initLock = new object();

        private static void EnsureLoaded()
        {
            if (_initialized) return;

            lock (_initLock)
            {
                if (_initialized) return;

                if (!Environment.Is64BitProcess)
                {
                    throw new PlatformNotSupportedException("tls-client-windows-64.dll requires a 64-bit process architecture.");
                }

                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string binDir = Path.Combine(baseDir, "bin");
                string dllInBin = Path.Combine(binDir, DllName);
                string dllInBase = Path.Combine(baseDir, DllName);

                if (Directory.Exists(binDir))
                {
                    SetDllDirectory(binDir);
                }

                if (File.Exists(dllInBin))
                {
                    _dllHandle = LoadLibrary(dllInBin);
                }
                else if (File.Exists(dllInBase))
                {
                    _dllHandle = LoadLibrary(dllInBase);
                }
                else
                {
                    _dllHandle = LoadLibrary(DllName);
                }

                if (_dllHandle == IntPtr.Zero)
                {
                    int lastError = Marshal.GetLastWin32Error();
                    throw new DllNotFoundException($"Failed to load native library '{DllName}' (Win32 Error: {lastError}). Ensure it exists in '{binDir}' or '{baseDir}'.");
                }

                _initialized = true;
            }
        }

        private static IntPtr StringToUtf8Ptr(string str)
        {
            if (str == null) return IntPtr.Zero;
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(str);
            IntPtr ptr = Marshal.AllocHGlobal(utf8Bytes.Length + 1);
            Marshal.Copy(utf8Bytes, 0, ptr, utf8Bytes.Length);
            Marshal.WriteByte(ptr, utf8Bytes.Length, 0); // Null terminator
            return ptr;
        }

        private static void FreeUtf8Ptr(IntPtr ptr)
        {
            if (ptr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(ptr);
            }
        }

        private static string PtrToStringUtf8(IntPtr ptr)
        {
            if (ptr == IntPtr.Zero) return null;
            int length = (int)strlen(ptr);
            if (length <= 0) return string.Empty;
            byte[] buffer = new byte[length];
            Marshal.Copy(ptr, buffer, 0, length);
            return Encoding.UTF8.GetString(buffer);
        }

        private static void FreeMemoryInternal(string memoryId)
        {
            if (string.IsNullOrEmpty(memoryId)) return;
            IntPtr memIdPtr = IntPtr.Zero;
            try
            {
                memIdPtr = StringToUtf8Ptr(memoryId);
                freeMemory(memIdPtr);
            }
            catch { }
            finally
            {
                FreeUtf8Ptr(memIdPtr);
            }
        }

        public static TlsClientResponsePayload Execute(TlsClientRequestPayload payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            EnsureLoaded();

            // Mandatory requirement: WithoutCookieJar must always be true
            // C# BotData.Cookies manages cookies; prevents >8KB header accumulation and HTTP 431/400 errors
            payload.WithoutCookieJar = true;

            string reqJson = JsonConvert.SerializeObject(payload);
            IntPtr reqPtr = IntPtr.Zero;
            IntPtr resPtr = IntPtr.Zero;

            try
            {
                reqPtr = StringToUtf8Ptr(reqJson);
                resPtr = request(reqPtr);
            }
            catch (DllNotFoundException)
            {
                throw new FileNotFoundException($"The native library '{DllName}' could not be found. Ensure it exists in the application root or bin directory.");
            }
            finally
            {
                FreeUtf8Ptr(reqPtr);
            }

            if (resPtr == IntPtr.Zero)
            {
                throw new Exception("Native TLS client returned a null pointer.");
            }

            string resJson = PtrToStringUtf8(resPtr);
            if (string.IsNullOrEmpty(resJson))
            {
                throw new Exception("Native TLS client returned an empty response string.");
            }

            TlsClientResponsePayload response = null;
            try
            {
                response = JsonConvert.DeserializeObject<TlsClientResponsePayload>(resJson);
            }
            finally
            {
                // Free the memory on the Go side for this response ID if present
                if (response != null && !string.IsNullOrEmpty(response.Id))
                {
                    FreeMemoryInternal(response.Id);
                }
            }

            // Decode byte response or data URI if returned
            if (response != null && !string.IsNullOrEmpty(response.Body))
            {
                if (payload.IsByteResponse || (response.Body.StartsWith("data:", StringComparison.OrdinalIgnoreCase) && response.Body.Contains(";base64,")))
                {
                    try
                    {
                        string b64 = response.Body;
                        int commaIdx = b64.IndexOf(',');
                        if (commaIdx >= 0)
                        {
                            b64 = b64.Substring(commaIdx + 1);
                        }
                        response.RawBytes = Convert.FromBase64String(b64);
                        response.Body = Encoding.UTF8.GetString(response.RawBytes);
                    }
                    catch { }
                }
            }

            return response;
        }

        public static void DestroySession(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return;

            IntPtr payloadPtr = IntPtr.Zero;
            try
            {
                string payload = JsonConvert.SerializeObject(new { sessionId = sessionId });
                payloadPtr = StringToUtf8Ptr(payload);
                IntPtr resPtr = destroySession(payloadPtr);
                if (resPtr != IntPtr.Zero)
                {
                    string resJson = PtrToStringUtf8(resPtr);
                    if (!string.IsNullOrEmpty(resJson))
                    {
                        var resObj = JsonConvert.DeserializeObject<TlsClientResponsePayload>(resJson);
                        if (resObj != null && !string.IsNullOrEmpty(resObj.Id))
                        {
                            FreeMemoryInternal(resObj.Id);
                        }
                    }
                }
            }
            catch { }
            finally
            {
                FreeUtf8Ptr(payloadPtr);
            }
        }

        public static void DestroyAllSessions()
        {
            try
            {
                IntPtr resPtr = destroyAll();
                if (resPtr != IntPtr.Zero)
                {
                    string resJson = PtrToStringUtf8(resPtr);
                    if (!string.IsNullOrEmpty(resJson))
                    {
                        var resObj = JsonConvert.DeserializeObject<TlsClientResponsePayload>(resJson);
                        if (resObj != null && !string.IsNullOrEmpty(resObj.Id))
                        {
                            FreeMemoryInternal(resObj.Id);
                        }
                    }
                }
            }
            catch { }
        }
    }
}
