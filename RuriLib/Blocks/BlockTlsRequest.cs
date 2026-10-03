using Extreme.Net;
using RuriLib.Functions.Files;
using RuriLib.Functions.Formats;
using RuriLib.Functions.Requests;
using RuriLib.Functions.Requests.TlsClient;
using RuriLib.LS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Media;
using MultipartContent = RuriLib.Functions.Requests.MultipartContent;

namespace RuriLib.Blocks
{
    /// <summary>
    /// High-performance request block that bypasses modern WAF and anti-bot systems
    /// by impersonating browser TLS (JA3/JA4) and HTTP/2 protocol signatures.
    /// </summary>
    public class BlockTlsRequest : BlockBase
    {
        #region Variables
        private string url = "https://google.com";
        /// <summary>The URL to call, including query parameters.</summary>
        public string Url { get { return url; } set { url = value; OnPropertyChanged(); } }

        private HttpMethod method = HttpMethod.GET;
        /// <summary>The HTTP method.</summary>
        public HttpMethod Method { get { return method; } set { method = value; OnPropertyChanged(); } }

        private string tlsIdentifier = TlsClientProfiles.DefaultProfile;
        /// <summary>The browser profile to emulate (e.g. chrome_131, chrome_133, firefox_133, safari_18).</summary>
        public string TlsIdentifier { get { return tlsIdentifier; } set { tlsIdentifier = value; OnPropertyChanged(); } }

        private bool autoHeader = true;
        /// <summary>Whether to automatically inject browser-matching sec-ch-ua, Accept, and language headers.</summary>
        public bool AutoHeader { get { return autoHeader; } set { autoHeader = value; OnPropertyChanged(); } }

        private bool randomTlsExtensionOrder = true;
        /// <summary>Whether to randomize the TLS extension order for enhanced fingerprint evasion.</summary>
        public bool RandomTlsExtensionOrder { get { return randomTlsExtensionOrder; } set { randomTlsExtensionOrder = value; OnPropertyChanged(); } }

        private bool insecureSkipVerify = false;
        /// <summary>Whether to ignore SSL/TLS certificate errors.</summary>
        public bool InsecureSkipVerify { get { return insecureSkipVerify; } set { insecureSkipVerify = value; OnPropertyChanged(); } }

        private int timeoutSeconds = 30;
        /// <summary>Request timeout in seconds.</summary>
        public int TimeoutSeconds { get { return timeoutSeconds; } set { timeoutSeconds = value; OnPropertyChanged(); } }

        private RequestType requestType = RequestType.Standard;
        /// <summary>The request type.</summary>
        public RequestType RequestType { get { return requestType; } set { requestType = value; OnPropertyChanged(); } }

        // Basic Auth
        private string authUser = "";
        public string AuthUser { get { return authUser; } set { authUser = value; OnPropertyChanged(); } }

        private string authPass = "";
        public string AuthPass { get { return authPass; } set { authPass = value; OnPropertyChanged(); } }

        // Standard Content
        private string postData = "";
        public string PostData { get { return postData; } set { postData = value; OnPropertyChanged(); } }

        private string contentType = "application/x-www-form-urlencoded";
        public string ContentType { get { return contentType; } set { contentType = value; OnPropertyChanged(); } }

        // Raw Content
        private string rawData = "";
        public string RawData { get { return rawData; } set { rawData = value; OnPropertyChanged(); } }

        // Multipart Content
        private string multipartBoundary = "";
        public string MultipartBoundary { get { return multipartBoundary; } set { multipartBoundary = value; OnPropertyChanged(); } }
        public List<MultipartContent> MultipartContents { get; set; } = new List<MultipartContent>();

        // Headers and Cookies
        public Dictionary<string, string> CustomHeaders { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> CustomCookiesList { get; set; } = new List<string>();

        private bool autoRedirect = true;
        public bool AutoRedirect { get { return autoRedirect; } set { autoRedirect = value; OnPropertyChanged(); } }

        private bool readResponseSource = true;
        public bool ReadResponseSource { get { return readResponseSource; } set { readResponseSource = value; OnPropertyChanged(); } }

        private ResponseType responseType = ResponseType.String;
        public ResponseType ResponseType { get { return responseType; } set { responseType = value; OnPropertyChanged(); } }

        private string downloadPath = "";
        public string DownloadPath { get { return downloadPath; } set { downloadPath = value; OnPropertyChanged(); } }

        private string outputVariable = "";
        public string OutputVariable { get { return outputVariable; } set { outputVariable = value; OnPropertyChanged(); } }

        private bool saveAsScreenshot = false;
        public bool SaveAsScreenshot { get { return saveAsScreenshot; } set { saveAsScreenshot = value; OnPropertyChanged(); } }
        #endregion

        public BlockTlsRequest()
        {
            Label = "TLS REQUEST";
        }

        public override BlockBase FromLS(string line)
        {
            var input = line.Trim();

            if (input.StartsWith("#"))
                Label = LineParser.ParseLabel(ref input);

            Method = (HttpMethod)LineParser.ParseEnum(ref input, "Method", typeof(HttpMethod));
            Url = LineParser.ParseLiteral(ref input, "URL");

            while (LineParser.Lookahead(ref input) == TokenType.Boolean)
                LineParser.SetBool(ref input, this);

            CustomHeaders.Clear();
            CustomCookiesList.Clear();

            while (input != string.Empty && !input.StartsWith("->"))
            {
                if (LineParser.Lookahead(ref input) == TokenType.Boolean)
                {
                    LineParser.SetBool(ref input, this);
                    continue;
                }

                var parsed = LineParser.ParseToken(ref input, TokenType.Parameter, true);
                switch (parsed.ToUpperInvariant())
                {
                    case "TLSIDENTIFIER":
                    case "TLSID":
                    case "PROFILE":
                        TlsIdentifier = LineParser.ParseLiteral(ref input, "TlsIdentifier");
                        break;
                    case "CONTENTTYPE":
                    case "TYPE":
                        ContentType = LineParser.ParseLiteral(ref input, "ContentType");
                        break;
                    case "CONTENT":
                        PostData = LineParser.ParseLiteral(ref input, "Content");
                        break;
                    case "TIMEOUT":
                        TimeoutSeconds = LineParser.ParseInt(ref input, "Timeout");
                        break;
                    case "INSECURE":
                        InsecureSkipVerify = bool.Parse(LineParser.ParseToken(ref input, TokenType.Parameter, true));
                        break;
                    case "RANDOMEXT":
                        RandomTlsExtensionOrder = bool.Parse(LineParser.ParseToken(ref input, TokenType.Parameter, true));
                        break;
                    case "REQUESTTYPE":
                        RequestType = (RequestType)LineParser.ParseEnum(ref input, "RequestType", typeof(RequestType));
                        break;
                    case "AUTHUSER":
                        AuthUser = LineParser.ParseLiteral(ref input, "AuthUser");
                        break;
                    case "AUTHPASS":
                        AuthPass = LineParser.ParseLiteral(ref input, "AuthPass");
                        break;
                    case "COOKIE":
                        CustomCookiesList.Add(LineParser.ParseLiteral(ref input, "Cookie"));
                        break;
                    case "HEADER":
                        var headerLine = LineParser.ParseLiteral(ref input, "Header");
                        if (headerLine.Contains(":"))
                        {
                            var split = headerLine.Split(new[] { ':' }, 2);
                            CustomHeaders[split[0].Trim()] = split[1].Trim();
                        }
                        break;
                }
            }

            if (LineParser.ParseToken(ref input, TokenType.Arrow, false) == string.Empty)
                return this;

            var respType = LineParser.ParseToken(ref input, TokenType.Parameter, true);
            switch (respType.ToUpperInvariant())
            {
                case "STRING":
                    ResponseType = ResponseType.String;
                    break;
                case "FILE":
                    ResponseType = ResponseType.File;
                    DownloadPath = LineParser.ParseLiteral(ref input, "DownloadPath");
                    break;
                case "BASE64":
                    ResponseType = ResponseType.Base64String;
                    OutputVariable = LineParser.ParseLiteral(ref input, "OutputVariable");
                    break;
            }

            return this;
        }

        public override string ToLS(bool indent = true)
        {
            var writer = new BlockWriter(GetType(), indent, Disabled);
            writer
                .Label(Label)
                .Token("TLSREQUEST")
                .Token(Method)
                .Literal(Url);

            if (!writer.CheckDefault(AutoRedirect, "AutoRedirect"))
                writer.Boolean(AutoRedirect, "AutoRedirect");

            if (!writer.CheckDefault(ReadResponseSource, "ReadResponseSource"))
                writer.Boolean(ReadResponseSource, "ReadResponseSource");

            if (!writer.CheckDefault(AutoHeader, "AutoHeader"))
                writer.Boolean(AutoHeader, "AutoHeader");

            if (!writer.CheckDefault(TlsIdentifier, "TlsIdentifier"))
                writer.Indent().Token("TLSID").Literal(TlsIdentifier);

            if (TimeoutSeconds != 30 && TimeoutSeconds > 0)
                writer.Indent().Token("TIMEOUT").Integer(TimeoutSeconds);

            if (InsecureSkipVerify)
                writer.Indent().Token("INSECURE").Token(InsecureSkipVerify.ToString().ToUpper());

            if (!RandomTlsExtensionOrder)
                writer.Indent().Token("RANDOMEXT").Token(RandomTlsExtensionOrder.ToString().ToUpper());

            if (RequestType != RequestType.Standard)
            {
                writer.Indent().Token("REQUESTTYPE").Token(RequestType.ToString());
                if (RequestType == RequestType.BasicAuth)
                {
                    writer.Indent().Token("AUTHUSER").Literal(AuthUser).Token("AUTHPASS").Literal(AuthPass);
                }
            }

            if (!string.IsNullOrEmpty(PostData))
            {
                writer
                    .Indent()
                    .Token("CONTENT")
                    .Literal(PostData)
                    .Indent()
                    .Token("CONTENTTYPE")
                    .Literal(ContentType);
            }

            foreach (var c in CustomCookiesList)
            {
                writer
                    .Indent()
                    .Token("COOKIE")
                    .Literal(c);
            }

            foreach (var h in CustomHeaders)
            {
                writer
                    .Indent()
                    .Token("HEADER")
                    .Literal($"{h.Key}: {h.Value}");
            }

            if (ResponseType == ResponseType.File)
            {
                writer.Indent().Arrow().Token("FILE").Literal(DownloadPath);
            }
            else if (ResponseType == ResponseType.Base64String)
            {
                writer.Indent().Arrow().Token("BASE64").Literal(OutputVariable);
            }

            return writer.ToString();
        }

        public override void Process(BotData data)
        {
            base.Process(data);

            var localUrl = ReplaceValues(Url, data);
            data.Log(new LogEntry($"Calling URL via TLS Engine ({TlsIdentifier}): {localUrl}", Colors.Cyan));

            // Use persistent session per bot to maintain HTTP/2 Keep-Alive and prevent clientMap memory bloat
            string sessionId = !string.IsNullOrEmpty(data.TlsSessionId) ? data.TlsSessionId : $"bot_{data.BotNumber}";

            var payload = new TlsClientRequestPayload
            {
                SessionId = sessionId,
                RequestUrl = localUrl,
                RequestMethod = Method.ToString().ToUpperInvariant(),
                TlsClientIdentifier = ReplaceValues(TlsIdentifier, data),
                FollowRedirects = AutoRedirect,
                InsecureSkipVerify = InsecureSkipVerify,
                WithRandomTLSExtensionOrder = RandomTlsExtensionOrder,
                TimeoutSeconds = TimeoutSeconds > 0 ? TimeoutSeconds : (data.GlobalSettings?.General?.RequestTimeout ?? 30),
                IsByteResponse = ResponseType == ResponseType.File,
                WithoutCookieJar = true // Single source of truth is C# BotData.Cookies
            };

            // Configure Proxy
            if (data.UseProxies && data.Proxy != null)
            {
                string protocol = "http";
                switch (data.Proxy.Type)
                {
                    case ProxyType.Http:
                        protocol = "http";
                        break;
                    case ProxyType.Socks4:
                        protocol = "socks4";
                        break;
                    case ProxyType.Socks5:
                        protocol = "socks5";
                        break;
                }

                string credentials = "";
                if (!string.IsNullOrEmpty(data.Proxy.Username))
                {
                    credentials = $"{Uri.EscapeDataString(data.Proxy.Username)}:{Uri.EscapeDataString(data.Proxy.Password ?? "")}@";
                }

                payload.ProxyUrl = $"{protocol}://{credentials}{data.Proxy.Proxy}";
                data.Log(new LogEntry($"Using Proxy ({protocol}): {data.Proxy.Proxy}", Colors.SlateGray));
            }

            // Headers setup
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var headerOrder = new List<string>();

            if (AutoHeader)
            {
                TlsClientProfiles.ApplyBrowserHeaders(payload.TlsClientIdentifier, headers, headerOrder);

                try
                {
                    var uri = new Uri(localUrl);
                    if (Method == HttpMethod.POST || !string.IsNullOrEmpty(PostData) || localUrl.IndexOf("/api/", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        headers["Sec-Fetch-Site"] = "same-origin";
                        headers["Sec-Fetch-Mode"] = "cors";
                        headers["Sec-Fetch-Dest"] = "empty";
                        headers.Remove("Upgrade-Insecure-Requests");
                        headers.Remove("Sec-Fetch-User");
                        headerOrder.RemoveAll(h => string.Equals(h, "Upgrade-Insecure-Requests", StringComparison.OrdinalIgnoreCase) ||
                                                   string.Equals(h, "Sec-Fetch-User", StringComparison.OrdinalIgnoreCase));

                        if (!headers.ContainsKey("Origin"))
                            headers["Origin"] = $"{uri.Scheme}://{uri.Authority}";
                        if (!headers.ContainsKey("Referer"))
                            headers["Referer"] = $"{uri.Scheme}://{uri.Authority}/";
                    }
                }
                catch { }
            }

            foreach (var h in CustomHeaders)
            {
                string key = ReplaceValues(h.Key, data);
                string val = ReplaceValues(h.Value, data);
                headers[key] = val;
                if (!headerOrder.Any(existing => string.Equals(existing, key, StringComparison.OrdinalIgnoreCase)))
                {
                    headerOrder.Add(key);
                }
            }

            // Ensure User-Agent is ALWAYS present even if AutoHeader is false and CustomHeaders omitted it
            if (!headers.ContainsKey("User-Agent"))
            {
                string ua = !string.IsNullOrEmpty(data.ConfigSettings?.CustomUserAgent) 
                    ? data.ConfigSettings.CustomUserAgent 
                    : "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";
                headers["User-Agent"] = ua;
                if (!headerOrder.Any(h => string.Equals(h, "User-Agent", StringComparison.OrdinalIgnoreCase)))
                {
                    headerOrder.Add("User-Agent");
                }
            }

            // Cookies setup
            ApplyCustomCookies(CustomCookiesList, data);

            if (data.Cookies != null && data.Cookies.Count > 0)
            {
                data.Log(new LogEntry("Sent Cookies:", Colors.MediumTurquoise));
                foreach (var c in data.Cookies)
                {
                    data.Log(new LogEntry($"{c.Key}: {c.Value}", Colors.LightGoldenrodYellow));
                }

                try
                {
                    var uri = new Uri(localUrl);
                    string targetHost = uri.Host;

                    var cleanCookies = new List<string>(data.Cookies.Count);
                    foreach (var cookie in data.Cookies)
                    {
                        if (string.IsNullOrEmpty(cookie.Key)) continue;

                        // Sanitize values to prevent HTTP header splitting
                        string cleanVal = (cookie.Value ?? "").Replace("\r", "").Replace("\n", "");
                        cleanCookies.Add($"{cookie.Key}={cleanVal}");
                    }

                    headers["Cookie"] = string.Join("; ", cleanCookies);
                    if (!headerOrder.Any(h => string.Equals(h, "Cookie", StringComparison.OrdinalIgnoreCase)))
                    {
                        headerOrder.Add("Cookie");
                    }
                }
                catch { }
            }

            // Request body setup
            switch (RequestType)
            {
                case RequestType.Standard:
                    if (HttpRequest.CanContainRequestBody(Method))
                    {
                        payload.RequestBody = ReplaceValues(PostData, data);
                        string ct = ReplaceValues(ContentType, data);
                        if (!string.IsNullOrEmpty(ct))
                        {
                            headers["Content-Type"] = ct;
                            if (!headerOrder.Any(h => string.Equals(h, "Content-Type", StringComparison.OrdinalIgnoreCase))) headerOrder.Add("Content-Type");
                        }
                    }
                    break;

                case RequestType.BasicAuth:
                    string auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{ReplaceValues(AuthUser, data)}:{ReplaceValues(AuthPass, data)}"));
                    headers["Authorization"] = $"Basic {auth}";
                    if (!headerOrder.Any(h => string.Equals(h, "Authorization", StringComparison.OrdinalIgnoreCase))) headerOrder.Add("Authorization");
                    break;

                case RequestType.Raw:
                    if (HttpRequest.CanContainRequestBody(Method))
                    {
                        payload.RequestBody = ReplaceValues(RawData, data);
                        string ct = ReplaceValues(ContentType, data);
                        if (!string.IsNullOrEmpty(ct))
                        {
                            headers["Content-Type"] = ct;
                            if (!headerOrder.Any(h => string.Equals(h, "Content-Type", StringComparison.OrdinalIgnoreCase))) headerOrder.Add("Content-Type");
                        }
                    }
                    break;

                case RequestType.Multipart:
                    string boundary = string.IsNullOrEmpty(MultipartBoundary) ? "----WebKitFormBoundary" + Guid.NewGuid().ToString("N").Substring(0, 16) : ReplaceValues(MultipartBoundary, data);
                    headers["Content-Type"] = $"multipart/form-data; boundary={boundary}";
                    if (!headerOrder.Any(h => string.Equals(h, "Content-Type", StringComparison.OrdinalIgnoreCase))) headerOrder.Add("Content-Type");

                    using (var ms = new MemoryStream())
                    {
                        byte[] boundaryBytes = Encoding.UTF8.GetBytes($"--{boundary}\r\n");
                        byte[] endBoundaryBytes = Encoding.UTF8.GetBytes($"--{boundary}--\r\n");
                        byte[] crlf = Encoding.UTF8.GetBytes("\r\n");

                        foreach (var mc in MultipartContents)
                        {
                            ms.Write(boundaryBytes, 0, boundaryBytes.Length);
                            if (mc.Type == MultipartContentType.String)
                            {
                                byte[] headerBytes = Encoding.UTF8.GetBytes($"Content-Disposition: form-data; name=\"{ReplaceValues(mc.Name, data)}\"\r\n\r\n");
                                ms.Write(headerBytes, 0, headerBytes.Length);
                                byte[] valBytes = Encoding.UTF8.GetBytes(ReplaceValues(mc.Value, data));
                                ms.Write(valBytes, 0, valBytes.Length);
                                ms.Write(crlf, 0, crlf.Length);
                            }
                            else if (mc.Type == MultipartContentType.File)
                            {
                                string filePath = ReplaceValues(mc.Value, data);
                                byte[] headerBytes = Encoding.UTF8.GetBytes($"Content-Disposition: form-data; name=\"{ReplaceValues(mc.Name, data)}\"; filename=\"{Path.GetFileName(filePath)}\"\r\nContent-Type: {mc.ContentType}\r\n\r\n");
                                ms.Write(headerBytes, 0, headerBytes.Length);
                                if (File.Exists(filePath))
                                {
                                    byte[] fileBytes = File.ReadAllBytes(filePath);
                                    ms.Write(fileBytes, 0, fileBytes.Length);
                                }
                                ms.Write(crlf, 0, crlf.Length);
                            }
                        }
                        ms.Write(endBoundaryBytes, 0, endBoundaryBytes.Length);
                        // Convert multipart payload to binary-safe raw string
                        payload.RequestBody = Encoding.GetEncoding("iso-8859-1").GetString(ms.ToArray());
                    }
                    break;
            }

            payload.Headers = headers;
            payload.HeaderOrder = headerOrder;

            if (headers != null && headers.Count > 0)
            {
                data.Log(new LogEntry("Sent Headers:", Colors.DarkTurquoise));
                foreach (var h in headers)
                {
                    data.Log(new LogEntry($"{h.Key}: {h.Value}", Colors.LightPink));
                }
            }

            // Execute request
            TlsClientResponsePayload response = null;
            try
            {
                response = TlsClientNative.Execute(payload);
            }
            catch (Exception ex)
            {
                try { TlsClientNative.DestroySession(sessionId); } catch { }
                if (data.ConfigSettings.IgnoreResponseErrors)
                {
                    data.Log(new LogEntry($"TLS Request failed: {ex.Message}", Colors.Tomato));
                    data.ResponseSource = ex.Message;
                    return;
                }
                throw;
            }

            if (response == null)
            {
                throw new Exception("Null response returned by TLS engine.");
            }

            // Sync address and status
            data.Address = response.Target ?? localUrl;
            data.ResponseCode = response.Status.ToString();

            // Sync headers
            var flatHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var receivedCookiesList = new List<KeyValuePair<string, string>>();

            if (response.Headers != null)
            {
                foreach (var h in response.Headers)
                {
                    flatHeaders[h.Key] = string.Join("; ", h.Value);

                    // Parse Set-Cookie headers into data.Cookies
                    if (string.Equals(h.Key, "Set-Cookie", StringComparison.OrdinalIgnoreCase) && h.Value != null)
                    {
                        foreach (var rawCookie in h.Value)
                        {
                            if (string.IsNullOrWhiteSpace(rawCookie)) continue;
                            var parts = rawCookie.Split(';');
                            if (parts.Length > 0)
                            {
                                var firstPart = parts[0].Trim();
                                int eqIdx = firstPart.IndexOf('=');
                                if (eqIdx > 0)
                                {
                                    string cKey = firstPart.Substring(0, eqIdx).Trim();
                                    string cVal = firstPart.Substring(eqIdx + 1).Trim();
                                    if (!string.IsNullOrEmpty(cKey))
                                    {
                                        data.Cookies[cKey] = cVal;
                                        receivedCookiesList.Add(new KeyValuePair<string, string>(cKey, cVal));
                                    }
                                }
                            }
                        }
                    }
                }
            }
            data.ResponseHeaders = flatHeaders;

            // Sync received cookies from response.Cookies (fallback if present)
            if (response.Cookies != null)
            {
                foreach (var c in response.Cookies)
                {
                    data.Cookies[c.Key] = c.Value;
                    if (!receivedCookiesList.Any(existing => existing.Key == c.Key))
                    {
                        receivedCookiesList.Add(new KeyValuePair<string, string>(c.Key, c.Value));
                    }
                }
            }

            // Decompress response body
            string body = response.Body ?? "";
            string contentEncoding = flatHeaders.ContainsKey("Content-Encoding") ? flatHeaders["Content-Encoding"] : null;
            string respContentType = flatHeaders.ContainsKey("Content-Type") ? flatHeaders["Content-Type"] : null;

            if (!string.IsNullOrWhiteSpace(contentEncoding))
            {
                if (response.RawBytes != null && response.RawBytes.Length > 0)
                {
                    var (decompBody, decompBytes) = HttpCompression.Decompress(response.RawBytes, contentEncoding, respContentType);
                    body = decompBody;
                    response.RawBytes = decompBytes;
                }
                else if (!string.IsNullOrEmpty(body))
                {
                    var (decompBody, decompBytes) = HttpCompression.DecompressString(body, contentEncoding, respContentType);
                    body = decompBody;
                    response.RawBytes = decompBytes;
                }
            }

            // Set complete body for PARSE, KEYCHECK, REGEX blocks
            data.ResponseSource = body;

            // Handle output formats
            switch (ResponseType)
            {
                case ResponseType.String:
                    break;

                case ResponseType.File:
                    string outPath = ReplaceValues(DownloadPath, data);
                    string outDir = Path.GetDirectoryName(outPath);
                    if (!string.IsNullOrEmpty(outDir) && !Directory.Exists(outDir))
                    {
                        Directory.CreateDirectory(outDir);
                    }
                    if (response.RawBytes != null && response.RawBytes.Length > 0)
                    {
                        File.WriteAllBytes(outPath, response.RawBytes);
                    }
                    else
                    {
                        File.WriteAllText(outPath, body, Encoding.UTF8);
                    }
                    data.Log(new LogEntry($"Saved response to {outPath}", Colors.Green));
                    break;

                case ResponseType.Base64String:
                    string b64 = Convert.ToBase64String(response.RawBytes ?? Encoding.UTF8.GetBytes(body));
                    InsertVariable(data, false, b64, OutputVariable);
                    break;
            }

            // Log details
            data.Log(new LogEntry("Address: " + data.Address, Colors.Cyan));
            data.Log(new LogEntry($"Response code: {data.ResponseCode}", Colors.Cyan));

            if (flatHeaders.Count > 0)
            {
                data.Log(new LogEntry("Received headers:", Colors.DeepPink));
                foreach (var h in flatHeaders)
                {
                    data.Log(new LogEntry($"{h.Key}: {h.Value}", Colors.LightPink));
                }
            }

            if (receivedCookiesList.Count > 0)
            {
                data.Log(new LogEntry("Received cookies:", Colors.Goldenrod));
                foreach (var c in receivedCookiesList)
                {
                    data.Log(new LogEntry($"{c.Key}: {c.Value}", Colors.LightGoldenrodYellow));
                }
            }

            data.Log(new LogEntry("Response Source:", Colors.Green));
            if (ReadResponseSource)
            {
                data.Log(new LogEntry(body, Colors.GreenYellow));
            }
            else
            {
                data.Log(new LogEntry("[SKIPPED]", Colors.GreenYellow));
            }
        }

        #region Helpers
        public string GetCustomCookies()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var pair in CustomCookiesList)
            {
                sb.Append(pair);
                if (!pair.Equals(CustomCookiesList.Last())) sb.Append(Environment.NewLine);
            }
            return sb.ToString();
        }

        public void SetCustomCookies(string[] lines)
        {
            CustomCookiesList.Clear();
            CustomCookiesList.AddRange(lines);
        }

        private void ApplyCustomCookies(List<string> lines, BotData data)
        {
            var allLines = new List<string>();
            foreach (var line in lines)
            {
                var replaced = ReplaceValues(line, data);
                if (replaced.Contains("\n"))
                {
                    allLines.AddRange(replaced.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
                }
                else
                {
                    allLines.Add(replaced);
                }
            }

            foreach (var line in allLines)
            {
                if (line.Contains(':'))
                {
                    var split = line.Split(new[] { ':' }, 2);
                    data.Cookies[split[0].Trim()] = split[1].Trim();
                }
                else if (line.Contains('='))
                {
                    var split = line.Split(new[] { '=' }, 2);
                    data.Cookies[split[0].Trim()] = split[1].Trim();
                }
            }
        }

        public string GetCustomHeaders()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var pair in CustomHeaders)
            {
                sb.Append($"{pair.Key}: {pair.Value}");
                if (!pair.Equals(CustomHeaders.Last())) sb.Append(Environment.NewLine);
            }
            return sb.ToString();
        }

        public void SetCustomHeaders(string[] lines)
        {
            CustomHeaders.Clear();
            foreach (var line in lines)
            {
                if (line.Contains(':'))
                {
                    var split = line.Split(new[] { ':' }, 2);
                    CustomHeaders[split[0].Trim()] = split[1].Trim();
                }
            }
        }

        public string GetMultipartContents()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var c in MultipartContents)
            {
                sb.Append($"{c.Type.ToString().ToUpper()}: {c.Name}: {c.Value}");
                if (!c.Equals(MultipartContents.Last())) sb.Append(Environment.NewLine);
            }
            return sb.ToString();
        }

        public void SetMultipartContents(string[] lines)
        {
            MultipartContents.Clear();
            foreach (var line in lines)
            {
                try
                {
                    var split = line.Split(new[] { ':' }, 3);
                    MultipartContents.Add(new MultipartContent
                    {
                        Type = (MultipartContentType)Enum.Parse(typeof(MultipartContentType), split[0].Trim(), true),
                        Name = split[1].Trim(),
                        Value = split[2].Trim()
                    });
                }
                catch { }
            }
        }
        #endregion
    }
}
