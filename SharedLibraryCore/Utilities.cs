using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using Humanizer;
using Humanizer.Localisation;
using Microsoft.Extensions.Logging;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos.Meta;
using SharedLibraryCore.Formatting;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Localization;
using SharedLibraryCore.RCon;
using static SharedLibraryCore.Server;
using static Data.Models.Client.EFClient;
using static Data.Models.EFPenalty;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SharedLibraryCore
{
    public static class Utilities
    {
        // note: this is only to be used by classes not created by dependency injection
        public static ILogger DefaultLogger { get; set; }
#if DEBUG == true
        public static string OperatingDirectory => $"{Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)}{Path.DirectorySeparatorChar}";
#else
        public static string OperatingDirectory =>
            $"{Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)}{Path.DirectorySeparatorChar}..{Path.DirectorySeparatorChar}";
#endif
        public static Encoding EncodingType;
        public static Layout CurrentLocalization = new Layout(new Dictionary<string, string>());

        public static TimeSpan DefaultCommandTimeout { get; set; } = new(0, 0, Utilities.IsDevelopment ? 360 : 25);
        public static char[] DirectorySeparatorChars = { '\\', '/' };
        public static char CommandPrefix { get; set; } = '!';

        public static string ToStandardFormat(this DateTime? time) => time?.ToString("yyyy-MM-dd H:mm:ss UTC");
        public static string ToStandardFormat(this DateTime time) => time.ToString("yyyy-MM-dd H:mm:ss UTC");

        public static EFClient IW4MAdminClient(Server server = null)
        {
            return new EFClient
            {
                ClientId = 1,
                State = EFClient.ClientState.Connected,
                Level = Permission.Console,
                CurrentServer = server,
                CurrentAlias = new EFAlias
                {
                    Name = "IW4MAdmin"
                },
                AdministeredPenalties = new List<EFPenalty>()
            };
        }

        /// <summary>
        ///     fallback id for world events
        /// </summary>
        public const long WORLD_ID = -1;

        public static Dictionary<Permission, string> PermissionLevelOverrides { get; } = new ();

        //Remove words from a space delimited string
        public static string RemoveWords(this string str, int num)
        {
            if (str == null || str.Length == 0)
            {
                return "";
            }

            var newStr = string.Empty;
            var tmp = str.Split(' ');

            for (var i = 0; i < tmp.Length; i++)
                if (i >= num)
                {
                    newStr += tmp[i] + ' ';
                }

            return newStr;
        }

        /// <summary>
        ///     caps client name to the specified character length - 3
        ///     and adds ellipses to the end of the reamining client name
        /// </summary>
        /// <param name="str">client name</param>
        /// <param name="maxLength">max number of characters for the name</param>
        /// <returns></returns>
        public static string CapClientName(this string str, int maxLength)
        {
            return str.Length > maxLength ? $"{str.Substring(0, maxLength - 3)}..." : str;
        }

        public static Permission MatchPermission(string str)
        {
            var lookingFor = str.ToLower();

            for (var perm = Permission.User; perm < Permission.Console; perm++)
                if (lookingFor.Contains(perm.ToString().ToLower())
                    || lookingFor.Contains(CurrentLocalization
                        .LocalizationIndex[$"GLOBAL_PERMISSION_{perm.ToString().ToUpper()}"].ToLower()))
                {
                    return perm;
                }

            return Permission.Banned;
        }

        /// <summary>
        ///     Remove all IW Engine color codes
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
            str = Regex.Replace(str, @"\(Color::(.{1,16})\)", "");
            return str;
        }

        /// <summary>
        ///     returns a "fixed" string that prevents message truncation in IW4 (and probably other Q3 clients)
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string FixIW4ForwardSlash(this string str)
        {
            return str.Replace("//", "/ /");
        }

        public static string RemoveDiacritics(this string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in from c in normalizedString.EnumerateRunes()
                     let unicodeCategory = Rune.GetUnicodeCategory(c)
                     where unicodeCategory != UnicodeCategory.NonSpacingMark
                     select c)
            {
                stringBuilder.Append(c);
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        public static string FormatMessageForEngine(this string str, IRConParserConfiguration config)
        {
            if (config == null || string.IsNullOrEmpty(str))
            {
                return str;
            }

            var output = str;
            var colorCodeMatches = Regex.Matches(output, @"\(Color::(.{1,16})\)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
            foreach (var match in colorCodeMatches.Where(m => m.Success))
            {
                var key = match.Groups[1].ToString();
                output = output.Replace(match.Value, config.ColorCodeMapping.TryGetValue(key, out var code) ? code : "");
            }

            if (config.ShouldRemoveDiacritics)
            {
                output = output.RemoveDiacritics();
            }

            return output.FixIW4ForwardSlash();
        }

        private static readonly IList<string> ZmGameTypes = new[]
            { "zclassic", "zstandard", "zcleansed", "zgrief", "zom", "cmp" };

        /// <summary>
        ///     indicates if the given server is running a zombie game mode
        /// </summary>
        /// <param name="server"></param>
        /// <returns></returns>
        public static bool IsZombieServer(this Server server)
        {
            return new[] { Game.T4, Game.T5, Game.T6 }.Contains(server.GameName) &&
                   ZmGameTypes.Contains(server.Gametype.ToLower());
        }

        public static bool IsCodGame(this Server server)
        {
            return server.RconParser?.RConEngine == "COD";
        }

        /// <summary>
        ///     Get the color key corresponding to a given user level
        /// </summary>
        /// <param name="level">Specified player level</param>
        /// <param name="localizedLevel"></param>
        /// <returns></returns>
        public static string ConvertLevelToColor(Permission level, string localizedLevel)
        {
            // todo: make configurable
            var colorCode = level switch
            {
                Permission.Banned => "Red",
                Permission.Flagged => "Map",
                Permission.Owner => "Accent",
                Permission.User => "Yellow",
                Permission.Trusted => "Green",
                _ => "Pink"
            };

            return $"(Color::{colorCode}){localizedLevel ?? level.ToString()}";
        }

        public static string ToLocalizedLevelName(this Permission permission)
        {
            var localized =
                CurrentLocalization.LocalizationIndex[$"GLOBAL_PERMISSION_{permission.ToString().ToUpper()}"];
            return PermissionLevelOverrides.ContainsKey(permission) && PermissionLevelOverrides[permission] != permission.ToString()
                ? PermissionLevelOverrides[permission]
                : localized;
        }

        public static async Task<string> ProcessMessageToken(this Server server, IList<MessageToken> tokens, string str)
        {
            var RegexMatches = Regex.Matches(str, @"\{\{[A-Z]+\}\}", RegexOptions.IgnoreCase);
            foreach (Match M in RegexMatches)
            {
                var Match = M.Value;
                var Identifier = M.Value.Substring(2, M.Length - 4);

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

        /// <summary>
        ///     Get the full gametype name
        /// </summary>
        /// <param name="input">Shorthand gametype reported from server</param>
        /// <returns></returns>
        public static string GetLocalizedGametype(string input)
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
        ///     converts a string to numerical guid
        /// </summary>
        /// <param name="str">source string for guid</param>
        /// <param name="numberStyle">how to parse the guid</param>
        /// <param name="fallback">value to use if string is empty</param>
        /// <returns></returns>
        public static long ConvertGuidToLong(this string str, NumberStyles numberStyle, long? fallback = null)
        {
            // added for source games that provide the steam ID
            var match = Regex.Match(str, @"^STEAM_(\d):(\d):(\d+)$");
            if (match.Success)
            {
                var x = int.Parse(match.Groups[1].ToString());
                var y = int.Parse(match.Groups[2].ToString());
                var z = long.Parse(match.Groups[3].ToString());

                return z * 2 + 0x0110000100000000 + y;
            }

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
                    long.TryParse(str.Length > 16 ? str.Substring(0, 16) : str, numberStyle,
                        CultureInfo.InvariantCulture, out id);
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
        ///     determines if the guid provided appears to be a bot guid
        ///     "1277538174" - (Pluto?)WaW (T4)
        /// </summary>
        /// <param name="guid">value of the guid</param>
        /// <returns>true if is bot guid, otherwise false</returns>
        public static bool IsBotGuid(this string guid)
        {
            return guid.Contains("bot") || guid == "0" || guid == "1277538174";
        }

        /// <summary>
        ///     generates a numerical hashcode from a string value
        /// </summary>
        /// <param name="value">value string</param>
        /// <returns></returns>
        public static long GenerateGuidFromString(this string value)
        {
            return string.IsNullOrEmpty(value) ? -1 : GetStableHashCode(value.StripColors());
        }

        /// https://stackoverflow.com/questions/36845430/persistent-hashcode-for-strings
        public static int GetStableHashCode(this string str)
        {
            unchecked
            {
                var hash1 = 5381;
                var hash2 = hash1;

                for (var i = 0; i < str.Length && str[i] != '\0'; i += 2)
                {
                    hash1 = ((hash1 << 5) + hash1) ^ str[i];
                    if (i == str.Length - 1 || str[i + 1] == '\0')
                    {
                        break;
                    }

                    hash2 = ((hash2 << 5) + hash2) ^ str[i + 1];
                }

                return hash1 + hash2 * 1566083941;
            }
        }

        public static int? ConvertToIP(this string str)
        {
            var success = IPAddress.TryParse(str, out var ip);
            return success && ip.GetAddressBytes().Count(_byte => _byte == 0) != 4
                ? BitConverter.ToInt32(ip.GetAddressBytes(), 0)
                : null;
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

        public static TimeSpan ParseTimespan(this string input)
        {
            var expressionMatch = Regex.Match(input, @"([0-9]+)(\w+)");

            if (!expressionMatch.Success) // fallback to default tempban length of 1 hour
            {
                return new TimeSpan(1, 0, 0);
            }

            var lengthDenote = expressionMatch.Groups[2].ToString()[0];
            var length = int.Parse(expressionMatch.Groups[1].ToString());

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

        public static bool HasPermission<TEntity, TPermission>(this IEnumerable<string> permissionsSet, TEntity entity,
            TPermission permission) where TEntity : Enum where TPermission : Enum
        {
            if (permissionsSet == null)
            {
                return false;
            }
            
            var requiredPermission = $"{entity.ToString()}.{permission.ToString()}";
            var hasAllPermissions = permissionsSet.Any(p => p.Equals("*"));
            var permissionCheckResult = permissionsSet.Select(p =>
            {
                if (p.Equals(requiredPermission, StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }

                if (p.Equals($"-{requiredPermission}", StringComparison.InvariantCultureIgnoreCase))
                {
                    return false;
                }

                return (bool?)null;
            }).ToList();

            var permissionNegated = permissionCheckResult.Any(result => result.HasValue && !result.Value);

            if (permissionNegated)
            {
                return false;
            }

            return hasAllPermissions || permissionCheckResult.Any(result => result.HasValue && result.Value);
        }

        public static bool HasPermission<TEntity, TPermission>(this ApplicationConfiguration appConfig,
            Permission permissionLevel, TEntity entity,
            TPermission permission) where TEntity : Enum where TPermission : Enum
        {
            return appConfig.PermissionSets.ContainsKey(permissionLevel.ToString()) &&
                   HasPermission(appConfig.PermissionSets[permissionLevel.ToString()], entity, permission);
        }

        /// <summary>
        ///     returns a list of penalty types that should be shown across all profiles
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
                PenaltyType.Unflag
            };
        }

        /// <summary>
        ///     Helper extension that determines if a user is a privileged client
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static bool IsPrivileged(this EFClient p)
        {
            return p.Level > Permission.Flagged;
        }

        /// <summary>
        ///     prompt user to answer a yes/no question
        /// </summary>
        /// <param name="question">question to prompt the user with</param>
        /// <param name="description">description of the question's value</param>
        /// <param name="defaultValue">default value to set if no input is entered</param>
        /// <returns></returns>
        public static bool PromptBool(this string question, string description = null, bool defaultValue = true)
        {
            Console.Write($"{question}?{(string.IsNullOrEmpty(description) ? " " : $" ({description}) ")}[y/n]: ");
            var response = Console.ReadLine()?.ToLower().FirstOrDefault();
            return response != 0 ? response == 'y' : defaultValue;
        }

        /// <summary>
        ///     prompt user to make a selection
        /// </summary>
        /// <typeparam name="T">type of selection</typeparam>
        /// <param name="question">question to prompt the user with</param>
        /// <param name="defaultValue">default value to set if no input is entered</param>
        /// <param name="description">description of the question's value</param>
        /// <param name="selections">array of possible selections (should be able to convert to string)</param>
        /// <returns></returns>
        public static Tuple<int, T> PromptSelection<T>(this string question, T defaultValue, string description = null,
            params T[] selections)
        {
            var hasDefault = false;

            if (defaultValue != null)
            {
                hasDefault = true;
                selections = new[] { defaultValue }.Union(selections).ToArray();
            }

            Console.WriteLine($"{question}{(string.IsNullOrEmpty(description) ? "" : $" [{description}:]")}");
            Console.WriteLine(new string('=', 52));
            for (var index = 0; index < selections.Length; index++)
                Console.WriteLine($"{(hasDefault ? index : index + 1)}] {selections[index]}");
            Console.WriteLine(new string('=', 52));

            var selectionIndex = PromptInt(CurrentLocalization.LocalizationIndex["SETUP_PROMPT_MAKE_SELECTION"], null,
                hasDefault ? 0 : 1, selections.Length, hasDefault ? 0 : null);

            if (!hasDefault)
            {
                selectionIndex--;
            }

            var selection = selections[selectionIndex];

            return Tuple.Create(selectionIndex, selection);
        }

        /// <summary>
        ///     prompt user to enter a number
        /// </summary>
        /// <param name="question">question to prompt with</param>
        /// <param name="maxValue">maximum value to allow</param>
        /// <param name="minValue">minimum value to allow</param>
        /// <param name="defaultValue">default value to set the return value to</param>
        /// <param name="description">a description of the question's value</param>
        /// <returns>integer from user's input</returns>
        public static int PromptInt(this string question, string description = null, int minValue = 0,
            int maxValue = int.MaxValue, int? defaultValue = null)
        {
            Console.Write(
                $"{question}{(string.IsNullOrEmpty(description) ? "" : $" ({description})")}{(defaultValue == null ? "" : $" [{CurrentLocalization.LocalizationIndex["SETUP_PROMPT_DEFAULT"]} {defaultValue.Value.ToString()}]")}: ");
            int response;

            string InputOrDefault()
            {
                var input = Console.ReadLine();
                return string.IsNullOrEmpty(input) && defaultValue != null ? defaultValue.ToString() : input;
            }

            while (!int.TryParse(InputOrDefault(), out response) ||
                   response < minValue ||
                   response > maxValue)
            {
                var range = "";
                if (minValue != 0 || maxValue != int.MaxValue)
                {
                    range = $" [{minValue}-{maxValue}]";
                }

                Console.Write($"{CurrentLocalization.LocalizationIndex["SETUP_PROMPT_INT"]}{range}: ");
            }

            return response;
        }

        /// <summary>
        ///     prompt use to enter a string response
        /// </summary>
        /// <param name="question">question to prompt with</param>
        /// <param name="description">description of the question's value</param>
        /// <param name="defaultValue">default value to set the return value to</param>
        /// <returns></returns>
        public static string PromptString(this string question, string description = null, string defaultValue = null)
        {
            string InputOrDefault()
            {
                var input = Console.ReadLine();
                return string.IsNullOrEmpty(input) && defaultValue != null ? defaultValue : input;
            }

            string response;
            do
            {
                Console.Write(
                    $"{question}{(string.IsNullOrEmpty(description) ? "" : $" ({description})")}{(defaultValue == null ? "" : $" [{CurrentLocalization.LocalizationIndex["SETUP_PROMPT_DEFAULT"]} {defaultValue}]")}: ");
                response = InputOrDefault();
            } while (string.IsNullOrWhiteSpace(response) && response != defaultValue);

            return response;
        }

        public static Dictionary<string, string> DictionaryFromKeyValue(this string eventLine)
        {
            var values = eventLine.Substring(1).Split('\\');

            Dictionary<string, string> dict = null;

            if (values.Length > 1)
            {
                dict = new Dictionary<string, string>();
                for (var i = values.Length % 2 == 0 ? 0 : 1; i < values.Length; i += 2)
                    dict.Add(values[i], values[i + 1]);
            }

            return dict;
        }

        /* https://loune.net/2017/06/running-shell-bash-commands-in-net-core/ */
        public static string GetCommandLine(int pId)
        {
            var cmdProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c wmic process where processid={pId} get CommandLine",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            cmdProcess.Start();
            cmdProcess.WaitForExit();

            var cmdLine = cmdProcess.StandardOutput.ReadToEnd().Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
            cmdProcess.Dispose();

            return cmdLine.Length > 1 ? cmdLine[1] : cmdLine[0];
        }

        /// <summary>
        ///     indicates if the given log path is a remote (http) uri
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        public static bool IsRemoteLog(this string log)
        {
            return (log ?? "").StartsWith("http");
        }

        public static string ToBase64UrlSafeString(this string src)
        {
            return Convert.ToBase64String(src.Select(c => Convert.ToByte(c)).ToArray()).Replace('+', '-')
                .Replace('/', '_');
        }

        public static async Task<Dvar<T>> GetDvarAsync<T>(this Server server, string dvarName,
            T fallbackValue = default, CancellationToken token = default)
        {
            return await server.RconParser.GetDvarAsync(server.RemoteConnection, dvarName, fallbackValue, token);
        }

        public static void BeginGetDvar(this Server server, string dvarName, AsyncCallback callback, CancellationToken token = default)
        {
            server.RconParser.BeginGetDvar(server.RemoteConnection, dvarName, callback, token);
        }
        
        public static async Task<Dvar<T>> GetDvarAsync<T>(this Server server, string dvarName,
            T fallbackValue = default)
        {
            return await GetDvarAsync(server, dvarName, fallbackValue, default);
        }

        public static async Task<Dvar<T>> GetMappedDvarValueOrDefaultAsync<T>(this Server server, string dvarName,
            string infoResponseName = null, IDictionary<string, string> infoResponse = null,
            T overrideDefault = default, CancellationToken token = default)
        {
            // todo: unit test this
            var mappedKey = server.RconParser.GetOverrideDvarName(dvarName);
            var defaultValue = server.RconParser.GetDefaultDvarValue<T>(mappedKey) ?? overrideDefault;

            var foundKey = infoResponse?.Keys
                .Where(_key => new[] { mappedKey, dvarName, infoResponseName ?? dvarName }.Contains(_key))
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(foundKey))
            {
                return new Dvar<T>
                {
                    Value = (T)Convert.ChangeType(infoResponse[foundKey], typeof(T)),
                    Name = foundKey
                };
            }

            return await server.GetDvarAsync(mappedKey, defaultValue, token: token);
        }

        public static async Task SetDvarAsync(this Server server, string dvarName, object dvarValue, CancellationToken token = default)
        {
            await server.RconParser.SetDvarAsync(server.RemoteConnection, dvarName, dvarValue, token);
        }

        public static void BeginSetDvar(this Server server, string dvarName, object dvarValue,
            AsyncCallback callback, CancellationToken token = default)
        {
            server.RconParser.BeginSetDvar(server.RemoteConnection, dvarName, dvarValue, callback, token);
        }
        
        public static async Task SetDvarAsync(this Server server, string dvarName, object dvarValue)
        {
            await SetDvarAsync(server, dvarName, dvarValue, default);
        }

        public static async Task<string[]> ExecuteCommandAsync(this Server server, string commandName, CancellationToken token = default)
        {
            return await server.RconParser.ExecuteCommandAsync(server.RemoteConnection, commandName, token);
        }
        
        public static async Task<string[]> ExecuteCommandAsync(this Server server, string commandName)
        {
            return await ExecuteCommandAsync(server, commandName, default);
        }

        public static async Task<IStatusResponse> GetStatusAsync(this Server server, CancellationToken token)
        {
            try
            {
                return await server.RconParser.GetStatusAsync(server.RemoteConnection, token);
            }

            catch (TaskCanceledException)
            {
                return null;
            }
        }

        /// <summary>
        ///     Retrieves the key value pairs for server information usually checked after map rotation
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

            var response = await server.RemoteConnection.SendQueryAsync(StaticHelpers.QueryType.GET_INFO);
            var combinedResponse = response.Length > 1
                ? string.Join('\\', response.Where(r => r.Length > 0 && r[0] == '\\'))
                : response[0];
            return combinedResponse.DictionaryFromKeyValue();
        }

        public static double GetVersionAsDouble()
        {
            var version = Assembly.GetCallingAssembly().GetName().Version.ToString();
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
            var output = input;
            var index = 0;
            foreach (Match match in matches)
            {
                output = output.Replace(match.Value, $"{{{index.ToString()}}}");
                index++;
            }

            try
            {
                return string.Format(output, values);
            }
            catch
            {
                return input;
            }
        }

        /// <summary>
        ///     https://stackoverflow.com/questions/8113546/how-to-determine-whether-an-ip-address-in-private/39120248
        ///     An extension method to determine if an IP address is internal, as specified in RFC1918
        /// </summary>
        /// <param name="toTest">The IP address that will be tested</param>
        /// <returns>Returns true if the IP is internal, false if it is external</returns>
        public static bool IsInternal(this IPAddress toTest)
        {
            if (toTest.ToString().StartsWith("127.0.0"))
            {
                return true;
            }

            var bytes = toTest.GetAddressBytes();
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
        ///     retrieves the external IP address of the current running machine
        /// </summary>
        /// <returns></returns>
        public static async Task<string> GetExternalIP()
        {
            try
            {
                using var wc = new HttpClient();
                return await wc.GetStringAsync("https://api.ipify.org");
            }

            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Determines if the given message is a quick message
        /// </summary>
        /// <param name="message"></param>
        /// <returns>true if the </returns>
        public static bool IsQuickMessage(this string message)
        {
            return Regex.IsMatch(message, @"^\u0014(?:\w|_|!|\s)+$");
        }

        /// <summary>
        ///     trims new line and whitespace from string
        /// </summary>
        /// <param name="str">source string</param>
        /// <returns></returns>
        public static string TrimNewLine(this string str)
        {
            return str.Trim().TrimEnd('\r', '\n');
        }

        public static Vector3 FixIW4Angles(this Vector3 vector)
        {
            var X = vector.X >= 0 ? vector.X : 360.0f + vector.X;
            var Y = vector.Y >= 0 ? vector.Y : 360.0f + vector.Y;
            var Z = vector.Z >= 0 ? vector.Z : 360.0f + vector.Z;

            return new Vector3(Y, X, Z);
        }

        public static float ToRadians(this float value)
        {
            return (float)Math.PI * value / 180.0f;
        }

        public static float ToDegrees(this float value)
        {
            return value * 180.0f / (float)Math.PI;
        }

        public static double[] AngleStuff(Vector3 a, Vector3 b)
        {
            var deltaX = 180.0 - Math.Abs(Math.Abs(a.X - b.X) - 180.0);
            var deltaY = 180.0 - Math.Abs(Math.Abs(a.Y - b.Y) - 180.0);

            return new[] { deltaX, deltaY };
        }

        /// <summary>
        ///     attempts to create and persist a penalty
        /// </summary>
        /// <param name="penalty"></param>
        /// <param name="penaltyService"></param>
        /// <param name="logger"></param>
        /// <returns>true of the create succeeds, false otherwise</returns>
        public static async Task<bool> TryCreatePenalty(this EFPenalty penalty,
            IEntityService<EFPenalty> penaltyService, ILogger logger)
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
        ///     https://www.planetgeek.ch/2016/12/08/async-method-without-cancellation-support-do-it-my-way/
        /// </summary>
        public static async Task WithWaitCancellation(this Task task,
            CancellationToken cancellationToken)
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken));
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

        public static async Task<T> WithWaitCancellation<T>(this Task<T> task,
            CancellationToken cancellationToken)
        {
            var completedTask = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, cancellationToken));
            if (completedTask == task)
            {
                return await task;
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("Infinite delay task completed.");
        }

        public static async Task<T> WithTimeout<T>(this Task<T> task, TimeSpan timeout)
        {
            await Task.WhenAny(task, Task.Delay(timeout));
            return await task;
        }

        public static async Task WithTimeout(this Task task, TimeSpan timeout)
        {
            await Task.WhenAny(task, Task.Delay(timeout));
        }


        public static bool ShouldHideLevel(this Permission perm)
        {
            return perm == Permission.Flagged;
        }

        /// <summary>
        ///     parses translation string into tokens that are able to be formatted by the webfront
        /// </summary>
        /// <param name="translationKey">key for translation lookup</param>
        /// <returns></returns>
        public static WebfrontTranslationHelper[] SplitTranslationTokens(string translationKey)
        {
            var translationString = CurrentLocalization.LocalizationIndex[translationKey];
            var builder = new StringBuilder();
            var results = new List<WebfrontTranslationHelper>();

            foreach (var word in translationString.Split(' '))
            {
                var finalWord = word;

                if (word.StartsWith("{{") && !word.EndsWith("}}") ||
                    builder.Length > 0 && !word.EndsWith("}}"))
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
                var isInterpolation = match.Success;

                results.Add(new WebfrontTranslationHelper
                {
                    IsInterpolation = isInterpolation,
                    MatchValue = isInterpolation
                        ? match.Groups[3].Length > 0 ? match.Groups[3].ToString() : match.Groups[1].ToString()
                        : finalWord,
                    TranslationValue = isInterpolation && match.Groups[2].Length > 0 ? match.Groups[2].ToString() : ""
                });
            }

            return results.ToArray();
        }

        /// <summary>
        ///     indicates if running in development mode
        /// </summary>
        /// <returns></returns>
        public static bool IsDevelopment =>
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

        /// <summary>
        ///     replaces any directory separator chars with the platform specific character
        /// </summary>
        /// <param name="path">original file path</param>
        /// <returns></returns>
        public static string FixDirectoryCharacters(this string path)
        {
            foreach (var separator in DirectorySeparatorChars)
                path = (path ?? "").Replace(separator, Path.DirectorySeparatorChar);

            return path;
        }

        /// <summary>
        ///     wrapper method for humanizee that uses current current culture
        /// </summary>
        public static string HumanizeForCurrentCulture(this TimeSpan timeSpan, int precision = 1,
            TimeUnit maxUnit = TimeUnit.Week,
            TimeUnit minUnit = TimeUnit.Second, string collectionSeparator = ", ", bool toWords = false)
        {
            return timeSpan.Humanize(precision, CurrentLocalization.Culture, maxUnit, minUnit, collectionSeparator,
                toWords);
        }

        /// <summary>
        ///     wrapper method for humanizee that uses current current culture
        /// </summary>
        public static string HumanizeForCurrentCulture(this DateTime input, bool utcDate = true,
            DateTime? dateToCompareAgainst = null, CultureInfo culture = null)
        {
            return input.Humanize(utcDate, dateToCompareAgainst, CurrentLocalization.Culture);
        }

        public static string ToTranslatedName(this MetaType metaType)
        {
            return CurrentLocalization.LocalizationIndex[$"META_TYPE_{metaType.ToString().ToUpper()}_NAME"];
        }

        public static EFClient ToPartialClient(this Data.Models.Client.EFClient client)
        {
            return new EFClient
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
                Active = client.Active,
                GameName = client.GameName
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
            return value.ToString(
                "#,##0" + $"{(precision > 0 ? "." : "")}" + new string(Enumerable.Repeat('0', precision).ToArray()),
                CurrentLocalization.Culture);
        }

        public static string ToNumericalString(this double? value, int precision = 0)
        {
            return value?.ToNumericalString(precision);
        }

        public static string[] FragmentMessageForDisplay(this string message)
        {
            var messages = new List<string>();
            var length = 48;

            if (message.Length <= length)
            {
                return new[] { message };
            }

            int i;
            for (i = 0; i < message.Length - length; i += length)
                messages.Add(new string(message.Skip(i).Take(length).ToArray()));

            var left = message.Length - length;

            if (left > 0)
            {
                messages.Add(new string(message.Skip(i).Take(left).ToArray()));
            }

            return messages.ToArray();
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

        public static string MakeAbbreviation(string gameName) => string.Join("",
            gameName.Split(' ').Select(word => char.ToUpper(word.First())).ToArray());
    }
}
