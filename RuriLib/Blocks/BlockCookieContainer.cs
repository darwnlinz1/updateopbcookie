using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using RuriLib.LS;

namespace RuriLib.Blocks
{
    /// <summary>
    /// The block is designed to retrieve cookies from a file by domain
    /// </summary>
    public class BlockCookieContainer : BlockBase
    {
        private string variableName = "";
        /// <summary>The name of the output variable.</summary>
        public string VariableName { get { return variableName; } set { variableName = value; OnPropertyChanged(); } }

        private string domain = "google.com";
        /// <summary>The target domain to extract cookies for.</summary>
        public string Domain { get { return domain; } set { domain = value; OnPropertyChanged(); } }

        private string inputString = "";
        /// <summary>The path or variable pointing to the cookie file.</summary>
        public string InputString { get { return inputString; } set { inputString = value; OnPropertyChanged(); } }

        private bool saveNetscape = false;
        /// <summary>Option to save cookies in Netscape format.</summary>
        public bool SaveNetscape { get { return saveNetscape; } set { saveNetscape = value; OnPropertyChanged(); } }

        private bool failOnEmpty = false;
        /// <summary>Option to fail the bot if no cookies are found for the domain.</summary>
        public bool FailOnEmpty { get { return failOnEmpty; } set { failOnEmpty = value; OnPropertyChanged(); } }

        /// <summary>
        /// Create block
        /// </summary>
        public BlockCookieContainer()
        {
            Label = "COOKIE CONTAINER";
        }

        public override BlockBase FromLS(string line)
        {
            var input = line.Trim();

            // Parse the label
            if (input.StartsWith("#"))
                Label = LineParser.ParseLabel(ref input);

            Domain = LineParser.ParseLiteral(ref input, "DOMAIN");

            // Try to parse the input string
            if (LineParser.Lookahead(ref input) == TokenType.Literal)
                InputString = LineParser.ParseLiteral(ref input, "InputString");

            while (LineParser.Lookahead(ref input) == TokenType.Boolean)
                LineParser.SetBool(ref input, this);

            // Try to parse the arrow, otherwise just return the block as is with default var name and var / cap choice
            if (LineParser.ParseToken(ref input, TokenType.Arrow, false) == string.Empty)
                return this;

            // Parse the SAVE / NOTSAVE
            try
            {
                var varType = LineParser.ParseToken(ref input, TokenType.Parameter, true);
                if (varType.ToUpper() == "SAVE" || varType.ToUpper() == "NOTSAVE")
                    SaveNetscape = varType.ToUpper() == "SAVE";
            }
            catch { throw new ArgumentException("Invalid or missing variable type"); }

            // Parse the variable
            try { VariableName = LineParser.ParseToken(ref input, TokenType.Literal, true); }
            catch { throw new ArgumentException("Variable name not specified"); }

            return this;
        }

        public override string ToLS(bool indent = true)
        {
            var writer = new BlockWriter(GetType(), indent, Disabled);
            writer
                .Label(Label)
                .Token("COOKIECONTAINER")
                .Literal(Domain);

            writer.Literal(InputString, "InputString");

            if (FailOnEmpty)
                writer.Boolean(FailOnEmpty, "FailOnEmpty");

            if (!writer.CheckDefault(VariableName, "VariableName"))
                writer
                    .Arrow()
                    .Token(SaveNetscape ? "SAVE" : "NOTSAVE")
                    .Literal(VariableName);

            return writer.ToString();
        }

        private static string CleanDomain(string domain)
        {
            if (string.IsNullOrWhiteSpace(domain)) return "";
            string d = domain.Trim().ToLowerInvariant();
            if (d.StartsWith("http://")) d = d.Substring(7);
            else if (d.StartsWith("https://")) d = d.Substring(8);
            int slashIdx = d.IndexOf('/');
            if (slashIdx >= 0) d = d.Substring(0, slashIdx);
            int colonIdx = d.IndexOf(':');
            if (colonIdx >= 0) d = d.Substring(0, colonIdx);
            d = d.TrimStart('.');
            if (d.StartsWith("www.")) d = d.Substring(4);
            return d;
        }

        private static bool MatchesDomain(string cookieDomain, string targetDomain)
        {
            if (string.IsNullOrWhiteSpace(targetDomain) || targetDomain == "*" || targetDomain.Equals("all", StringComparison.OrdinalIgnoreCase))
                return true;

            if (string.IsNullOrWhiteSpace(cookieDomain)) return false;

            string cDom = CleanDomain(cookieDomain);
            string tDom = CleanDomain(targetDomain);

            if (string.IsNullOrEmpty(cDom) || string.IsNullOrEmpty(tDom)) return false;

            if (cDom.Equals(tDom, StringComparison.OrdinalIgnoreCase)) return true;
            if (cDom.EndsWith("." + tDom, StringComparison.OrdinalIgnoreCase)) return true;
            if (tDom.EndsWith("." + cDom, StringComparison.OrdinalIgnoreCase)) return true;

            if (!tDom.Contains("."))
            {
                if (cDom.StartsWith(tDom + ".", StringComparison.OrdinalIgnoreCase) ||
                    cDom.IndexOf("." + tDom + ".", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cDom.IndexOf(tDom, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }
            else
            {
                if (cDom.IndexOf(tDom, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        public override void Process(BotData data)
        {
            base.Process(data);

            StringBuilder builder = new StringBuilder();
            string outputCookiesString = "";

            var cookiepath = ReplaceValues(inputString, data);
            if (string.IsNullOrEmpty(cookiepath) || !File.Exists(cookiepath))
            {
                var candidates = new[] { "<COOKIEPATH>", "<DATA>", "<INPUT>", "<1>" };
                foreach (var cand in candidates)
                {
                    var resolved = ReplaceValues(cand, data);
                    if (!string.IsNullOrEmpty(resolved) && File.Exists(resolved))
                    {
                        cookiepath = resolved;
                        break;
                    }
                }
            }

            if (!File.Exists(cookiepath))
            {
                data.Log(new LogEntry($"Cookie file not found: '{cookiepath}'", Colors.Tomato));
                InsertVariable(data, false, "WRONGPATH", variableName);
                if (FailOnEmpty)
                {
                    data.Status = BotStatus.FAIL;
                }
                return;
            }

            string targetDomain = ReplaceValues(Domain, data).Trim();
            var cookiesKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (data.Cookies == null)
            {
                data.Cookies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                string rawFileContent = File.ReadAllText(cookiepath);

                // 1. Try parsing as JSON array if it begins with [
                if (rawFileContent.TrimStart().StartsWith("["))
                {
                    try
                    {
                        var jsonArray = JArray.Parse(rawFileContent);
                        foreach (var token in jsonArray)
                        {
                            if (token is JObject obj)
                            {
                                string cDomain = obj["domain"]?.ToString() ?? obj["host"]?.ToString() ?? "";
                                if (MatchesDomain(cDomain, targetDomain))
                                {
                                    string cName = obj["name"]?.ToString() ?? "";
                                    string cValue = obj["value"]?.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(cName) && !cookiesKeys.Contains(cName))
                                    {
                                        cookiesKeys.Add(cName);
                                        outputCookiesString += $"{cName}: {cValue}\n";
                                        data.Cookies[cName] = cValue;
                                        if (SaveNetscape)
                                        {
                                            builder.AppendLine($"{cDomain}\tTRUE\t/\tFALSE\t0\t{cName}\t{cValue}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                    catch { }
                }

                // 2. Fallback / Line-by-line Netscape parsing
                if (cookiesKeys.Count == 0)
                {
                    using (var reader = new StringReader(rawFileContent))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                            try
                            {
                                string[] splited = null;
                                if (line.Contains("\t"))
                                {
                                    splited = line.Split('\t');
                                }
                                else
                                {
                                    splited = System.Text.RegularExpressions.Regex.Split(line.Trim(), @"\s{2,}");
                                }

                                if (splited != null && splited.Length >= 7)
                                {
                                    string cDomain = splited[0];
                                    string cName = splited[5];
                                    string cValue = splited[6];

                                    if (MatchesDomain(cDomain, targetDomain) && !cookiesKeys.Contains(cName))
                                    {
                                        if (SaveNetscape) builder.AppendLine(line);
                                        cookiesKeys.Add(cName);
                                        outputCookiesString += $"{cName}: {cValue}\n";
                                        data.Cookies[cName] = cValue;
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                data.Log(new LogEntry($"Error reading cookie file: {ex.Message}", Colors.Tomato));
            }

            if (cookiesKeys.Count == 0)
            {
                data.Log(new LogEntry($"No valid cookies found for domain '{targetDomain}' in '{cookiepath}'.", Colors.OrangeRed));
                InsertVariable(data, false, "", variableName);
                if (SaveNetscape) InsertVariable(data, false, "", "COOKIENETSCAPE");

                if (FailOnEmpty)
                {
                    data.Status = BotStatus.FAIL;
                }
                return;
            }

            data.Log(new LogEntry($"Loaded {cookiesKeys.Count} cookies for domain '{targetDomain}' into session", Colors.Green));
            InsertVariable(data, false, outputCookiesString, variableName);
            if (SaveNetscape) InsertVariable(data, false, builder.ToString(), "COOKIENETSCAPE");
        }
    }
}
