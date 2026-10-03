using Extreme.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RuriLib.Functions.UserAgent
{
    /// <summary>
    /// Provides methods to generate User Agents.
    /// </summary>
    public static class UserAgent
    {
        /// <summary>
        /// Enumerates browsers for which a User Agent can be generated.
        /// </summary>
        public enum Browser
        {
            /// <summary>The Google Chrome browser.</summary>
            Chrome,

            /// <summary>The Mozilla Firefox browser.</summary>
            Firefox,

            /// <summary>The Internet Explorer browser.</summary>
            InternetExplorer,

            /// <summary>The Opera browser.</summary>
            Opera,

            /// <summary>The Opera Mini mobile browser.</summary>
            OperaMini
        }

        /// <summary>
        /// Generates a User Agent for a specific Browser.
        /// </summary>
        /// <param name="browser">The Browser</param>
        /// <returns>A User Agent for the given browser</returns>
        public static string ForBrowser(Browser browser)
        {
            switch (browser)
            {
                case Browser.Chrome:
                    return Http.ChromeUserAgent();

                case Browser.Firefox:
                    return Http.FirefoxUserAgent();

                case Browser.InternetExplorer:
                    return Http.IEUserAgent();

                case Browser.Opera:
                    return Http.OperaUserAgent();

                case Browser.OperaMini:
                    return Http.OperaMiniUserAgent();

                default:
                    throw new Exception("Browser not supported");
            }
        }

        /// <summary>
        /// Gets a random User-Agent header.
        /// </summary>
        /// <param name="rand">A random number generator</param>
        /// <returns>A randomly generated User-Agent header</returns>
        public static string Random(Random rand)
        {
            if (rand == null) rand = new Random();
            int roll = rand.Next(100);

            // Chrome Windows (131 - 134) = 65%
            if (roll < 65)
            {
                int ver = rand.Next(131, 135);
                int patch = rand.Next(100, 200);
                return $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{ver}.0.0.{patch} Safari/537.36";
            }
            // Chrome Mac / Linux = 15%
            else if (roll < 80)
            {
                int ver = rand.Next(131, 134);
                return $"Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{ver}.0.0.0 Safari/537.36";
            }
            // Firefox Windows (132 - 134) = 10%
            else if (roll < 90)
            {
                int ver = rand.Next(132, 135);
                return $"Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:{ver}.0) Gecko/20100101 Firefox/{ver}.0";
            }
            // Safari macOS (17 - 18) = 5%
            else if (roll < 95)
            {
                return "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15";
            }
            // Edge Windows (131 - 133) = 5%
            else
            {
                int ver = rand.Next(131, 134);
                return $"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/{ver}.0.0.0 Safari/537.36 Edg/{ver}.0.0.0";
            }
        }
    }
}
