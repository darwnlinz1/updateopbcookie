using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace RuriLib.Functions.Requests.TlsClient
{
    public class TlsCookie
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        [JsonProperty("domain")]
        public string Domain { get; set; }

        [JsonProperty("path")]
        public string Path { get; set; } = "/";
    }

    public class TlsClientRequestPayload
    {
        [JsonProperty("sessionId")]
        public string SessionId { get; set; }

        [JsonProperty("tlsClientIdentifier")]
        public string TlsClientIdentifier { get; set; } = "chrome_131";

        [JsonProperty("followRedirects")]
        public bool FollowRedirects { get; set; } = true;

        [JsonProperty("insecureSkipVerify")]
        public bool InsecureSkipVerify { get; set; } = false;

        [JsonProperty("withoutCookieJar")]
        public bool WithoutCookieJar { get; set; } = true;

        [JsonProperty("withRandomTLSExtensionOrder")]
        public bool WithRandomTLSExtensionOrder { get; set; } = true;

        [JsonProperty("timeoutSeconds")]
        public int TimeoutSeconds { get; set; } = 30;

        [JsonProperty("timeoutMilliseconds", NullValueHandling = NullValueHandling.Ignore)]
        public int? TimeoutMilliseconds { get; set; } = null;

        [JsonProperty("proxyUrl", NullValueHandling = NullValueHandling.Ignore)]
        public string ProxyUrl { get; set; }

        [JsonProperty("isByteResponse")]
        public bool IsByteResponse { get; set; } = false;

        [JsonProperty("headerOrder", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> HeaderOrder { get; set; }

        [JsonProperty("headers", NullValueHandling = NullValueHandling.Ignore)]
        public Dictionary<string, string> Headers { get; set; }

        [JsonProperty("requestCookies", NullValueHandling = NullValueHandling.Ignore)]
        public List<TlsCookie> RequestCookies { get; set; } = new List<TlsCookie>();

        [JsonProperty("requestMethod")]
        public string RequestMethod { get; set; } = "GET";

        [JsonProperty("requestUrl")]
        public string RequestUrl { get; set; }

        [JsonProperty("requestBody", NullValueHandling = NullValueHandling.Ignore)]
        public string RequestBody { get; set; }
    }

    public class TlsClientResponsePayload
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("status")]
        public int Status { get; set; }

        [JsonProperty("target")]
        public string Target { get; set; }

        [JsonProperty("body")]
        public string Body { get; set; }

        [JsonProperty("headers")]
        public Dictionary<string, List<string>> Headers { get; set; }

        [JsonProperty("cookies")]
        public Dictionary<string, string> Cookies { get; set; }

        [JsonIgnore]
        public byte[] RawBytes { get; set; }
    }

    public static class TlsClientProfiles
    {
        public const string DefaultProfile = "chrome_131";

        public static readonly string[] AvailableProfiles = new[]
        {
            "chrome_133",
            "chrome_131",
            "chrome_130",
            "chrome_124",
            "chrome_120",
            "firefox_133",
            "firefox_132",
            "firefox_120",
            "safari_18",
            "safari_17",
            "safari_ios_18",
            "safari_ios_17",
            "opera_90"
        };

        public static void ApplyBrowserHeaders(string profile, Dictionary<string, string> headers, List<string> headerOrder)
        {
            if (headers == null) return;
            string p = profile?.ToLowerInvariant() ?? DefaultProfile;

            if (p.StartsWith("chrome") || p.StartsWith("opera"))
            {
                string chromeVer = "131";
                if (p.Contains("133")) chromeVer = "133";
                else if (p.Contains("130")) chromeVer = "130";
                else if (p.Contains("124")) chromeVer = "124";
                else if (p.Contains("120")) chromeVer = "120";

                headers["sec-ch-ua"] = $"\"Google Chrome\";v=\"{chromeVer}\", \"Chromium\";v=\"{chromeVer}\", \"Not_A Brand\";v=\"24\"";
                headers["sec-ch-ua-mobile"] = "?0";
                headers["sec-ch-ua-platform"] = "\"Windows\"";
                headers["Upgrade-Insecure-Requests"] = "1";
                headers["User-Agent"] = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{chromeVer}.0.0.0 Safari/537.36";
                headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7";
                headers["Sec-Fetch-Site"] = "none";
                headers["Sec-Fetch-Mode"] = "navigate";
                headers["Sec-Fetch-User"] = "?1";
                headers["Sec-Fetch-Dest"] = "document";
                headers["Accept-Encoding"] = "gzip, deflate, br, zstd";
                headers["Accept-Language"] = "en-US,en;q=0.9";

                if (headerOrder != null)
                {
                    headerOrder.AddRange(new[]
                    {
                        "sec-ch-ua",
                        "sec-ch-ua-mobile",
                        "sec-ch-ua-platform",
                        "Upgrade-Insecure-Requests",
                        "User-Agent",
                        "Accept",
                        "Sec-Fetch-Site",
                        "Sec-Fetch-Mode",
                        "Sec-Fetch-User",
                        "Sec-Fetch-Dest",
                        "Accept-Encoding",
                        "Accept-Language"
                    });
                }
            }
            else if (p.StartsWith("firefox"))
            {
                string ffVer = p.Contains("133") ? "133.0" : (p.Contains("132") ? "132.0" : "120.0");
                headers["User-Agent"] = $"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:{ffVer}) Gecko/20100101 Firefox/{ffVer}";
                headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8";
                headers["Accept-Language"] = "en-US,en;q=0.5";
                headers["Accept-Encoding"] = "gzip, deflate, br, zstd";
                headers["Upgrade-Insecure-Requests"] = "1";
                headers["Sec-Fetch-Dest"] = "document";
                headers["Sec-Fetch-Mode"] = "navigate";
                headers["Sec-Fetch-Site"] = "none";
                headers["Sec-Fetch-User"] = "?1";

                if (headerOrder != null)
                {
                    headerOrder.AddRange(new[]
                    {
                        "User-Agent",
                        "Accept",
                        "Accept-Language",
                        "Accept-Encoding",
                        "Upgrade-Insecure-Requests",
                        "Sec-Fetch-Dest",
                        "Sec-Fetch-Mode",
                        "Sec-Fetch-Site",
                        "Sec-Fetch-User"
                    });
                }
            }
            else if (p.StartsWith("safari"))
            {
                headers["User-Agent"] = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15";
                headers["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
                headers["Accept-Language"] = "en-US,en;q=0.9";
                headers["Accept-Encoding"] = "gzip, deflate, br";

                if (headerOrder != null)
                {
                    headerOrder.AddRange(new[]
                    {
                        "User-Agent",
                        "Accept",
                        "Accept-Language",
                        "Accept-Encoding"
                    });
                }
            }
        }
    }
}
