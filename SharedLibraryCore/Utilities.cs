
using Humanizer;
using Humanizer.Localisation;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos.Meta;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Configuration;
using static SharedLibraryCore.Server;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using static Data.Models.Client.EFClient;
using Data.Models;
using static Data.Models.EFPenalty;

namespace SharedLibraryCore
{
    public static class Utilities
    {
        // note: this is only to be used by classes not created by dependency injection
        public static ILogger DefaultLogger { get; set; }
#if DEBUG == true
        public static string OperatingDirectory => $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}{Path.DirectorySeparatorChar}";
#else
        public static string OperatingDirectory => $"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}";
#endif
        public static Encoding EncodingType;
        public static Localization.Layout CurrentLocalization = new Localization.Layout(new Dictionary<string, string>());
        public static TimeSpan DefaultCommandTimeout { get; set; } = new TimeSpan(0, 0, 25);
        public static char[] DirectorySeparatorChars = new[] { '\\', '/' };
        public static char CommandPrefix { get; set; } = '!';
        public static EFClient IW4MAdminClient(Server server = null)
        {
            return new EFClient()
            {
                ClientId = 1,
                State = EFClient.ClientState.Connected,
                Level = EFClient.Permission.Console,
                CurrentServer = server,
                CurrentAlias = new EFAlias()
                {
                    Name = "IW4MAdmin"
                },
                AdministeredPenalties = new List<EFPenalty>()
            };
        }
        /// <summary>
        /// fallback id for world events
        /// </summary>
        public const long WORLD_ID = -1;
        public static Dictionary<Permission, string> PermissionLevelOverrides { get; } = new Dictionary<Permission, string>();

        public static string HttpRequest(string location, string header, string headerValue)
        {
            using (var RequestClient = new System.Net.Http.HttpClient())
            {
                RequestClient.DefaultRequestHeaders.Add(header, headerValue);
                string response = RequestClient.GetStringAsync(location).Result;
                return response;
            }
        }

        //Get string with specified number of spaces -- really only for visual output
        public static String GetSpaces(int Num)
        {
            String SpaceString = String.Empty;
            while (Num > 0)
            {
                SpaceString += ' ';
                Num--;
            }

            return SpaceString;
        }

        //Remove words from a space delimited string
        public static String RemoveWords(this string str, int num)
        {
            if (str == null || str.Length == 0)
            {
                return "";
            }

            String newStr = String.Empty;
            String[] tmp = str.Split(' ');

            for (int i = 0; i < tmp.Length; i++)
            {
                if (i >= num)
                {
                    newStr += tmp[i] + ' ';
                }
            }

            return newStr;
        }

        /// <summary>
        /// caps client name to the specified character length - 3
        /// and adds ellipses to the end of the reamining client name
        /// </summary>
        /// <param name="str">client name</param>
        /// <param name="maxLength">max number of characters for the name</param>
        /// <returns></returns>
        public static string CapClientName(this string str, int maxLength) =>
            str.Length > maxLength ?
            $"{str.Substring(0, maxLength - 3)}..." :
            str;

        /// <summary>
        /// helper method to get the information about an exception and inner exceptions
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        public static string GetExceptionInfo(this Exception ex)
        {
            var sb = new StringBuilder();
            int depth = 0;
            while (ex != null)
            {
                sb.AppendLine($"Exception[{depth}] Name: {ex.GetType().FullName}");
                sb.AppendLine($"Exception[{depth}] Message: {ex.Message}");
                sb.AppendLine($"Exception[{depth}] Call Stack: {ex.StackTrace}");
                sb.AppendLine($"Exception[{depth}] Source: {ex.Source}");
                depth++;
                ex = ex.InnerException;
            }

            return sb.ToString();
        }

        public static EFClient.Permission MatchPermission(String str)
        {
            String lookingFor = str.ToLower();

            for (EFClient.Permission Perm = EFClient.Permission.User; Perm < EFClient.Permission.Console; Perm++)
            {
                if (lookingFor.Contains(Perm.ToString().ToLower())
                    || lookingFor.Contains(CurrentLocalization.LocalizationIndex[$"GLOBAL_PERMISSION_{Perm.ToString().ToUpper()}"].ToLower()))
                {
                    return Perm;
                }
            }

            return EFClient.Permission.Banned;
        }

        /// <summary>
        /// Remove all IW Engine color codes
        /// </summary>
        /// <param name="str">String containing color codes</param>
        /// <returns></returns>
        public static string StripColors(this string str)
        {
            if (str == null)
            {
                return "";
            }

            str = Regex.Replace(str, @"(\^+((?![a-z]|[A-Z]).){0,1})+", "");
            return str;
        }

        /// <summary>
        /// returns a "fixed" string that prevents message truncation in IW4 (and probably other Q3 clients)
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string FixIW4ForwardSlash(this string str) => str.Replace("//", "/ /");

        private static readonly IList<string> _zmGameTypes = new[] { "zclassic", "zstandard", "zcleansed", "zgrief" };
        /// <summary>
        /// indicates if the given server is running a zombie game mode
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        public static bool IsZombieServer(this Server server) => server.GameName == Game.T6 && _zmGameTypes.Contains(server.Gametype.ToLower());

        /// <summary>
        /// Get the IW Engine color code corresponding to an admin level
        /// </summary>
        /// <param name="level">Specified player level</param>
        /// <returns></returns>
        public static String ConvertLevelToColor(EFClient.Permission level, string localizedLevel)
        {
            char colorCode = '6';
            // todo: maybe make this game independant?
            switch (level)
            {
                case EFClient.Permission.Banned:
                    colorCode = '1';
                    break;
                case EFClient.Permission.Flagged:
                    colorCode = '9';
                    break;
                case EFClient.Permission.Owner:
                    colorCode = '5';
                    break;
                case EFClient.Permission.User:
                    colorCode = '2';
                    break;
                case EFClient.Permission.Trusted:
                    colorCode = '3';
                    break;
                default:
                    break;
            }

            return $"^{colorCode}{localizedLevel ?? level.ToString()}";
        }

        public static string ToLocalizedLevelName(this Permission permission)
        {
            var localized = CurrentLocalization.LocalizationIndex[$"GLOBAL_PERMISSION_{permission.ToString().ToUpper()}"];
            return PermissionLevelOverrides.ContainsKey(permission) && PermissionLevelOverrides[permission] != localized
                ? PermissionLevelOverrides[permission]
                : localized;
        }

        public async static Task<string> ProcessMessageToken(this Server server, IList<Helpers.MessageToken> tokens, String str)
        {
            MatchCollection RegexMatches = Regex.Matches(str, @"\{\{[A-Z]+\}\}", RegexOptions.IgnoreCase);
            foreach (Match M in RegexMatches)
            {
                String Match = M.Value;
                String Identifier = M.Value.Substring(2, M.Length - 4);

                var found = tokens.FirstOrDefault(t => t.Name.ToLower() == Identifier.ToLower());

                if (found != null)
                {
                    str = str.Replace(Match, await found.ProcessAsync(server));
                }
            }

            return str;
        }

        public static bool IsBroadcastCommand(this string str, string broadcastCommandPrefix)
        {
            return str.StartsWith(broadcastCommandPrefix);
        }

        public static IManagerCommand AsCommand(this GameEvent gameEvent)
        {
            return gameEvent.Extra as IManagerCommand;
        }

        /// <summary>
        /// Get the full gametype name
        /// </summary>
        /// <param name="input">Shorthand gametype reported from server</param>
        /// <returns></returns>
        public static string GetLocalizedGametype(String input)
        {
            switch (input)
            {
                case "dm":
                    return "Deathmatch";
                case "war":
                    return "Team Deathmatch";
                case "koth":
                    return "Headquarters";
                case "ctf":
                    return "Capture The Flag";
                case "dd":
                    return "Demolition";
                case "dom":
                    return "Domination";
                case "sab":
                    return "Sabotage";
                case "sd":
                    return "Search & Destroy";
                case "vip":
                    return "Very Important Person";
                case "gtnw":
                    return "Global Thermonuclear War";
                case "oitc":
                    return "One In The Chamber";
                case "arena":
                    return "Arena";
                case "dzone":
                    return "Drop Zone";
                case "gg":
                    return "Gun Game";
                case "snipe":
                    return "Sniping";
                case "ss":
                    return "Sharp Shooter";
                case "m40a3":
                    return "M40A3";
                case "fo":
                    return "Face Off";
                case "dmc":
                    return "Deathmatch Classic";
                case "killcon":
                    return "Kill Confirmed";
                case "oneflag":
                    return "One Flag CTF";
                default:
                    return input;
            }
        }

        /// <summary>
        /// converts a string to numerical guid
        /// </summary>
        /// <param name="str">source string for guid</param>
        /// <param name="numberStyle">how to parse the guid</param>
        /// <param name="fallback">value to use if string is empty</param>
        /// <returns></returns>
        public static long ConvertGuidToLong(this string str, NumberStyles numberStyle, long? fallback = null)
        {
            str = str.Substring(0, Math.Min(str.Length, 19));
            var parsableAsNumber = Regex.Match(str, @"([A-F]|[a-f]|[0-9])+").Value;

            if (string.IsNullOrWhiteSpace(str) && fallback.HasValue)
            {
                return fallback.Value;
            }

            long id;
            if (!string.IsNullOrEmpty(parsableAsNumber))
            {
                if (numberStyle == NumberStyles.Integer)
                {
                    long.TryParse(str, numberStyle, CultureInfo.InvariantCulture, out id);

                    if (id < 0)
                    {
                        id = (uint)id;
                    }
                }

                else
                {
                    long.TryParse(str.Length > 16 ? str.Substring(0, 16) : str, numberStyle, CultureInfo.InvariantCulture, out id);
                }
            }

            else
            {
                // this is a special case for when a real guid is not provided, so we generated it from another source
                id = str.GenerateGuidFromString();
            }

            if (id == 0)
            {
                throw new FormatException($"Could not parse client GUID - {str}");
            }

            return id;
        }

        /// <summary>
        /// determines if the guid provided appears to be a bot guid
        /// </summary>
        /// <param name="guid">value of the guid</param>
        /// <returns>true if is bot guid, otherwise false</returns>
        public static bool IsBotGuid(this string guid)
        {
            return guid.Contains("bot") || guid == "0";
        }

        /// <summary>
        /// generates a numerical hashcode from a string value
        /// </summary>
        /// <param name="value">value string</param>
        /// <returns></returns>
        public static long GenerateGuidFromString(this string value) => string.IsNullOrEmpty(value) ? -1 : GetStableHashCode(value.StripColors());

        /// https://stackoverflow.com/questions/36845430/persistent-hashcode-for-strings
        public static int GetStableHashCode(this string str)
        {
            unchecked
            {
                int hash1 = 5381;
                int hash2 = hash1;

                for (int i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }

        public static int? ConvertToIP(this string str)
        {
            bool success = IPAddress.TryParse(str, out IPAddress ip);
            return success && ip.GetAddressBytes().Count(_byte => _byte == 0) != 4 ?
                (int?)BitConverter.ToInt32(ip.GetAddressBytes(), 0) :
                null;
        }

        public static string ConvertIPtoString(this int? ip)
        {
            return !ip.HasValue ? "" : new IPAddress(BitConverter.GetBytes(ip.Value)).ToString();
        }

        public static Game GetGame(string gameName)
        {
            if (string.IsNullOrEmpty(gameName))
            {
                return Game.UKN;
            }

            if (gameName.Contains("IW4"))
            {
                return Game.IW4;
            }

            if (gameName.Contains("CoD4"))
            {
                return Game.IW3;
            }

            if (gameName.Contains("COD_WaW"))
            {
                return Game.T4;
            }

            if (gameName.Contains("T5"))
            {
                return Game.T5;
            }

            if (gameName.Contains("IW5"))
            {
                return Game.IW5;
            }

            if (gameName.Contains("COD_T6_S"))
            {
                return Game.T6;
            }

            return Game.UKN;
        }

        public static string EscapeMarkdown(this string markdownString)
        {
            return markdownString.Replace("<", "\\<").Replace(">", "\\>").Replace("|", "\\|");
        }

        public static TimeSpan ParseTimespan(this string input)
        {
            var expressionMatch = Regex.Match(input, @"([0-9]+)(\w+)");

            if (!expressionMatch.Success) // fallback to default tempban length of 1 hour
            {
                return new TimeSpan(1, 0, 0);
            }

            char lengthDenote = expressionMatch.Groups[2].ToString()[0];
            int length = Int32.Parse(expressionMatch.Groups[1].ToString());

            var loc = CurrentLocalization.LocalizationIndex;

            if (lengthDenote == char.ToLower(loc["GLOBAL_TIME_MINUTES"][0]))
            {
                return new TimeSpan(0, length, 0);
            }

            if (lengthDenote == char.ToLower(loc["GLOBAL_TIME_HOURS"][0]))
            {
                return new TimeSpan(length, 0, 0);
            }

            if (lengthDenote == char.ToLower(loc["GLOBAL_TIME_DAYS"][0]))
            {
                return new TimeSpan(length, 0, 0, 0);
            }

            if (lengthDenote == char.ToLower(loc["GLOBAL_TIME_WEEKS"][0]))
            {
                return new TimeSpan(length * 7, 0, 0, 0);
            }

            if (lengthDenote == char.ToLower(loc["GLOBAL_TIME_YEARS"][0]))
            {
                return new TimeSpan(length * 365, 0, 0, 0);
            }

            return new TimeSpan(1, 0, 0);
        }

        /// <summary>
        /// returns a list of penalty types that should be shown across all profiles
        /// </summary>
        /// <returns></returns>
        public static PenaltyType[] LinkedPenaltyTypes()
        {
            return new[]
            {
                PenaltyType.Ban,
                PenaltyType.Unban,
                PenaltyType.TempBan,
                PenaltyType.Flag,
                PenaltyType.Unflag,
            };
        }

        /// <summary>
        /// Helper extension that determines if a user is a privileged client
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static bool IsPrivileged(this EFClient p)
        {
            return p.Level > EFClient.Permission.Flagged;
        }

        /// <summary>
        /// prompt user to answer a yes/no question
        /// </summary>
        /// <param name="question">question to prompt the user with</param>
        /// <param name="description">description of the question's value</param>
        /// <param name="defaultValue">default value to set if no input is entered</param>
        /// <returns></returns>
        public static bool PromptBool(string question, string description = null, bool defaultValue = true)
        {
            Console.Write($"{question}?{(string.IsNullOrEmpty(description) ? " " : $" ({description}) ")}[y/n]: ");
            char response = Console.ReadLine().ToLower().FirstOrDefault();
            return response != 0 ? response == 'y' : defaultValue;
        }

        /// <summary>
        /// prompt user to make a selection
        /// </summary>
        /// <typeparam name="T">type of selection</typeparam>
        /// <param name="question">question to prompt the user with</param>
        /// <param name="defaultValue">default value to set if no input is entered</param>
        /// <param name="description">description of the question's value</param>
        /// <param name="selections">array of possible selections (should be able to convert to string)</param>
        /// <returns></returns>
        public static Tuple<int, T> PromptSelection<T>(string question, T defaultValue, string description = null, params T[] selections)
        {
            bool hasDefault = false;

            if (defaultValue != null)
            {
                hasDefault = true;
                selections = (new T[] { defaultValue }).Union(selections).ToArray();
            }

            Console.WriteLine($"{question}{(string.IsNullOrEmpty(description) ? "" : $" [{ description}:]")}");
            Console.WriteLine(new string('=', 52));
            for (int index = 0; index < selections.Length; index++)
            {
                Console.WriteLine($"{(hasDefault ? index : index + 1)}] {selections[index]}");
            }
            Console.WriteLine(new string('=', 52));

            int selectionIndex = PromptInt(CurrentLocalization.LocalizationIndex["SETUP_PROMPT_MAKE_SELECTION"], null, hasDefault ? 0 : 1, selections.Length, hasDefault ? 0 : (int?)null);

            if (!hasDefault)
            {
                selectionIndex--;
            }

            T selection = selections[selectionIndex];

            return Tuple.Create(selectionIndex, selection);
        }

        /// <summary>
        /// prompt user to enter a number
        /// </summary>
        /// <param name="question">question to prompt with</param>
        /// <param name="maxValue">maximum value to allow</param>
        /// <param name="minValue">minimum value to allow</param>
        /// <param name="defaultValue">default value to set the return value to</param>
        /// <param name="description">a description of the question's value</param>
        /// <returns>integer from user's input</returns>
        public static int PromptInt(this string question, string description = null, int minValue = 0, int maxValue = int.MaxValue, int? defaultValue = null)
        {
            Console.Write($"{question}{(string.IsNullOrEmpty(description) ? "" : $" ({description})")}{(defaultValue == null ? "" : $" [{CurrentLocalization.LocalizationIndex["SETUP_PROMPT_DEFAULT"]} {defaultValue.Value.ToString()}]")}: ");
            int response;

            string inputOrDefault()
            {
                string input = Console.ReadLine();
                return string.IsNullOrEmpty(input) && defaultValue != null ? defaultValue.ToString() : input;
            }

            while (!int.TryParse(inputOrDefault(), out response) ||
                response < minValue ||
                response > maxValue)
            {
                string range = "";
                if (minValue != 0 || maxValue != int.MaxValue)
                {
                    range = $" [{minValue}-{maxValue}]";
                }
                Console.Write($"{CurrentLocalization.LocalizationIndex["SETUP_PROMPT_INT"]}{range}: ");
            }

            return response;
        }

        /// <summary>
        /// prompt use to enter a string response
        /// </summary>
        /// <param name="question">question to prompt with</param>
        /// <param name="description">description of the question's value</param>
        /// <param name="defaultValue">default value to set the return value to</param>
        /// <returns></returns>
        public static string PromptString(string question, string description = null, string defaultValue = null)
        {
            string inputOrDefault()
            {
                string input = Console.ReadLine();
                return string.IsNullOrEmpty(input) && defaultValue != null ? defaultValue.ToString() : input;
            }

            string response;
            do
            {
                Console.Write($"{question}{(string.IsNullOrEmpty(description) ? "" : $" ({description})")}{(defaultValue == null ? "" : $" [{CurrentLocalization.LocalizationIndex["SETUP_PROMPT_DEFAULT"]} {defaultValue}]")}: ");
                response = inputOrDefault();
            } while (string.IsNullOrWhiteSpace(response) && response != defaultValue);

            return response;
        }

        public static Dictionary<string, string> DictionaryFromKeyValue(this string eventLine)
        {
            string[] values = eventLine.Substring(1).Split('\\');

            Dictionary<string, string> dict = null;

            if (values.Length > 1)
            {
                dict = new Dictionary<string, string>();
                for (int i = values.Length % 2 == 0 ? 0 : 1; i < values.Length; i += 2)
                {
                    dict.Add(values[i], values[i + 1]);
                }
            }

            return dict;
        }

        /* https://loune.net/2017/06/running-shell-bash-commands-in-net-core/ */
        public static string GetCommandLine(int pId)
        {
            var cmdProcess = new Process()
            {
                StartInfo = new ProcessStartInfo()
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c wmic process where processid={pId} get CommandLine",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            cmdProcess.Start();
            cmdProcess.WaitForExit();

            string[] cmdLine = cmdProcess.StandardOutput.ReadToEnd().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            cmdProcess.Dispose();

            return cmdLine.Length > 1 ? cmdLine[1] : cmdLine[0];
        }

        /// <summary>
        /// indicates if the given log path is a remote (http) uri
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        public static bool IsRemoteLog(this string log)
        {
            return (log ?? "").StartsWith("http");
        }

        public static string ToBase64UrlSafeString(this string src)
        {
            return Convert.ToBase64String(src.Select(c => Convert.ToByte(c)).ToArray()).Replace('+', '-').Replace('/', '_');
        }

        public static Task<Dvar<T>> GetDvarAsync<T>(this Server server, string dvarName, T fallbackValue = default)
        {
            return server.RconParser.GetDvarAsync(server.RemoteConnection, dvarName, fallbackValue);
        }

        public static async Task<Dvar<T>> GetMappedDvarValueOrDefaultAsync<T>(this Server server, string dvarName, string infoResponseName = null, IDictionary<string, string> infoResponse = null, T overrideDefault = default)
        {
            // todo: unit test this
            string mappedKey = server.RconParser.GetOverrideDvarName(dvarName);
            var defaultValue = server.RconParser.GetDefaultDvarValue<T>(mappedKey) ?? overrideDefault;

            string foundKey = infoResponse?.Keys.Where(_key => new[] { mappedKey, dvarName, infoResponseName ?? dvarName }.Contains(_key)).FirstOrDefault();

            if (!string.IsNullOrEmpty(foundKey))
            {
                return new Dvar<T>
                {
                    Value = (T)Convert.ChangeType(infoResponse[foundKey], typeof(T)),
                    Name = foundKey
                };
            }

            return await server.GetDvarAsync(mappedKey, defaultValue);
        }

        public static Task SetDvarAsync(this Server server, string dvarName, object dvarValue)
        {
            return server.RconParser.SetDvarAsync(server.RemoteConnection, dvarName, dvarValue);
        }

        public static async Task<string[]> ExecuteCommandAsync(this Server server, string commandName)
        {
            return await server.RconParser.ExecuteCommandAsync(server.RemoteConnection, commandName);
        }

        public static Task<(List<EFClient>, string, string)> GetStatusAsync(this Server server)
        {
            return server.RconParser.GetStatusAsync(server.RemoteConnection);
        }

        /// <summary>
        /// Retrieves the key value pairs for server information usually checked after map rotation
        /// </summary>
        /// <param name="server"></param>
        /// <param name="delay">How long to wait after the map has rotated to query</param>
        /// <returns></returns>
        public static async Task<IDictionary<string, string>> GetInfoAsync(this Server server, TimeSpan? delay = null)
        {
            if (delay != null)
            {
                await Task.Delay(delay.Value);
            }

            var response = await server.RemoteConnection.SendQueryAsync(RCon.StaticHelpers.QueryType.GET_INFO);
            string combinedResponse = response.Length > 1 ?
                string.Join('\\', response.Where(r => r.Length > 0 && r[0] == '\\')) :
                response[0];
            return combinedResponse.DictionaryFromKeyValue();
        }

        public static double GetVersionAsDouble()
        {
            string version = Assembly.GetCallingAssembly().GetName().Version.ToString();
            version = version.Replace(".", "");
            return double.Parse(version) / 1000.0;
        }

        public static string GetVersionAsString()
        {
            return Assembly.GetCallingAssembly().GetName().Version.ToString();
        }

        public static string FormatExt(this string input, params object[] values)
        {
            var matches = Regex.Matches(Regex.Unescape(input), @"{{\w+}}");
            string output = input;
            int index = 0;
            foreach (Match match in matches)
            {
                output = output.Replace(match.Value.ToString(), $"{{{index.ToString()}}}");
                index++;
            }

            try
            {
                return string.Format(output, values);
            }
            catch { return input; }
        }

        /// <summary>
        /// https://stackoverflow.com/questions/8113546/how-to-determine-whether-an-ip-address-in-private/39120248
        /// An extension method to determine if an IP address is internal, as specified in RFC1918
        /// </summary>
        /// <param name="toTest">The IP address that will be tested</param>
        /// <returns>Returns true if the IP is internal, false if it is external</returns>
        public static bool IsInternal(this IPAddress toTest)
        {
            if (toTest.ToString().StartsWith("127.0.0"))
            {
                return true;
            }

            byte[] bytes = toTest.GetAddressBytes();
            switch (bytes[0])
            {
                case 0:
                    return bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 0;
                case 10:
                    return true;
                case 172:
                    return bytes[1] < 32 && bytes[1] >= 16;
                case 192:
                    return bytes[1] == 168;
                default:
                    return false;
            }
        }

        /// <summary>
        /// retrieves the external IP address of the current running machine
        /// </summary>
        /// <returns></returns>
        public static async Task<string> GetExternalIP()
        {
            try
            {
                using (var wc = new WebClient())
                {
                    return await wc.DownloadStringTaskAsync("https://api.ipify.org");
                }
            }

            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Determines if the given message is a quick message
        /// </summary>
        /// <param name="message"></param>
        /// <returns>true if the </returns>
        public static bool IsQuickMessage(this string message)
        {
            return Regex.IsMatch(message, @"^\u0014(?:\w|_|!|\s)+$");
        }

        /// <summary>
        /// trims new line and whitespace from string
        /// </summary>
        /// <param name="str">source string</param>
        /// <returns></returns>
        public static string TrimNewLine(this string str) => str.Trim().TrimEnd('\r', '\n');

        public static Vector3 FixIW4Angles(this Vector3 vector)
        {
            float X = vector.X >= 0 ? vector.X : 360.0f + vector.X;
            float Y = vector.Y >= 0 ? vector.Y : 360.0f + vector.Y;
            float Z = vector.Z >= 0 ? vector.Z : 360.0f + vector.Z;

            return new Vector3(Y, X, Z);
        }

        public static float ToRadians(this float value) => (float)Math.PI * value / 180.0f;

        public static float ToDegrees(this float value) => value * 180.0f / (float)Math.PI;

        public static double[] AngleStuff(Vector3 a, Vector3 b)
        {
            double deltaX = 180.0 - Math.Abs(Math.Abs(a.X - b.X) - 180.0);
            double deltaY = 180.0 - Math.Abs(Math.Abs(a.Y - b.Y) - 180.0);

            return new[] { deltaX, deltaY };
        }

        /// <summary>
        /// attempts to create and persist a penalty
        /// </summary>
        /// <param name="penalty"></param>
        /// <param name="penaltyService"></param>
        /// <param name="logger"></param>
        /// <returns>true of the create succeeds, false otherwise</returns>
        public static async Task<bool> TryCreatePenalty(this EFPenalty penalty, IEntityService<EFPenalty> penaltyService, ILogger logger)
        {
            try
            {
                await penaltyService.Create(penalty);
                return true;
            }

            catch (Exception e)
            {
                logger.LogError(e, $"Could not create penalty of type {penalty.Type.ToString()}");
            }

            return false;
        }

        /// <summary>
        /// https://www.planetgeek.ch/2016/12/08/async-method-without-cancellation-support-do-it-my-way/
        /// </summary>
        public static async Task WithWaitCancellation(this Task task,
              CancellationToken cancellationToken)
        {
            Task completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken));
            if (completedTask == task)
            {
                await task;
            }
            else
            {
                cancellationToken.ThrowIfCancellationRequested();
                throw new InvalidOperationException("Infinite delay task completed.");
            }
        }

        public static bool ShouldHideLevel(this Permission perm) => perm == Permission.Flagged;

        /// <summary>
        /// parses translation string into tokens that are able to be formatted by the webfront
        /// </summary>
        /// <param name="translationKey">key for translation lookup</param>
        /// <returns></returns>
        public static WebfrontTranslationHelper[] SplitTranslationTokens(string translationKey)
        {
            string translationString = CurrentLocalization.LocalizationIndex[translationKey];
            var builder = new StringBuilder();
            var results = new List<WebfrontTranslationHelper>();

            foreach (string word in translationString.Split(' '))
            {
                string finalWord = word;

                if ((word.StartsWith("{{") && !word.EndsWith("}}")) ||
                    (builder.Length > 0 && !word.EndsWith("}}")))
                {
                    builder.Append($"{word} ");
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(word);
                    finalWord = builder.ToString();
                    builder.Clear();
                }

                var match = Regex.Match(finalWord, @"{{([^}|^-]+)(?:->)([^}]+)}}|{{([^}]+)}}");
                bool isInterpolation = match.Success;

                results.Add(new WebfrontTranslationHelper
                {
                    IsInterpolation = isInterpolation,
                    MatchValue = isInterpolation ? match.Groups[3].Length > 0 ? match.Groups[3].ToString() : match.Groups[1].ToString() : finalWord,
                    TranslationValue = isInterpolation && match.Groups[2].Length > 0 ? match.Groups[2].ToString() : ""
                });
            }

            return results.ToArray();
        }

        /// <summary>
        /// indicates if running in development mode
        /// </summary>
        /// <returns></returns>
        public static bool IsDevelopment => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        /// <summary>
        /// replaces any directory separator chars with the platform specific character
        /// </summary>
        /// <param name="path">original file path</param>
        /// <returns></returns>
        public static string FixDirectoryCharacters(this string path)
        {
            foreach (char separator in DirectorySeparatorChars)
            {
                path = (path ?? "").Replace(separator, Path.DirectorySeparatorChar);
            }

            return path;
        }

        /// <summary>
        /// wrapper method for humanizee that uses current current culture
        /// </summary>
        public static string HumanizeForCurrentCulture(this TimeSpan timeSpan, int precision = 1, TimeUnit maxUnit = TimeUnit.Week,
            TimeUnit minUnit = TimeUnit.Second, string collectionSeparator = ", ", bool toWords = false)
        {
            return timeSpan.Humanize(precision, CurrentLocalization.Culture, maxUnit, minUnit, collectionSeparator, toWords);
        }

        /// <summary>
        /// wrapper method for humanizee that uses current current culture
        /// </summary>
        public static string HumanizeForCurrentCulture(this DateTime input, bool utcDate = true, DateTime? dateToCompareAgainst = null, CultureInfo culture = null)
        {
            return input.Humanize(utcDate, dateToCompareAgainst, CurrentLocalization.Culture);
        }

        public static string ToTranslatedName(this MetaType metaType)
        {
            return CurrentLocalization.LocalizationIndex[$"META_TYPE_{metaType.ToString().ToUpper()}_NAME"];
        }

        public static EFClient ToPartialClient(this Data.Models.Client.EFClient client)
        {
            return new EFClient()
            {
                ClientId = client.ClientId,
                NetworkId = client.NetworkId,
                Connections = client.Connections,
                TotalConnectionTime = client.TotalConnectionTime,
                FirstConnection = client.FirstConnection,
                LastConnection = client.LastConnection,
                Masked = client.Masked,
                AliasLinkId = client.AliasLinkId,
                AliasLink = client.AliasLink,
                Level = client.Level,
                CurrentAliasId = client.CurrentAliasId,
                CurrentAlias = client.CurrentAlias,
                Password = client.Password,
                PasswordSalt = client.PasswordSalt,
                Meta = client.Meta,
                ReceivedPenalties = client.ReceivedPenalties,
                AdministeredPenalties = client.AdministeredPenalties,
                Active = client.Active
            };
        }

        public static string ToNumericalString(this int? value)
        {
            return value?.ToNumericalString();
        }

        public static string ToNumericalString(this int value)
        {
            return value.ToString("#,##0", CurrentLocalization.Culture);
        }
        
        public static string ToNumericalString(this double value, int precision = 0)
        {
            return value.ToString("#,##0" + $"{(precision > 0 ? "." : "")}" + new string(Enumerable.Repeat('0', precision).ToArray()), CurrentLocalization.Culture);
        }

        public static string ToNumericalString(this double? value, int precision = 0)
        {
            return value?.ToNumericalString(precision);
        }

        public static string FindRuleForReason(this string reason, ApplicationConfiguration appConfig, Server server)
        {
            // allow for penalty presets
            if (appConfig.PresetPenaltyReasons?.ContainsKey(reason.ToLower()) ?? false)
            {
                return appConfig.PresetPenaltyReasons[reason.ToLower()];
            }
            
            var regex = Regex.Match(reason, @"rule(\d+)", RegexOptions.IgnoreCase);
            if (!regex.Success)
            {
                return reason;
            }

            var serverConfig = appConfig.Servers?
                .FirstOrDefault(configServer =>
                    configServer.IPAddress == server.IP && configServer.Port == server.Port);

            var allRules = appConfig.GlobalRules?.ToList() ?? new List<string>();
            if (serverConfig?.Rules != null)
            {
                allRules.AddRange(serverConfig.Rules);
            }

            var index = int.Parse(regex.Groups[1].ToString()) - 1;

            if (!allRules.Any() || index > allRules.Count - 1 || index < 0)
            {
                return reason;
            }

            return allRules[index];
        }
    }
}
