using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

using SharedLibraryCore.Objects;
using static SharedLibraryCore.Server;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
using System.Diagnostics;

namespace SharedLibraryCore
{
    public static class Utilities
    {
        public static string OperatingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + Path.DirectorySeparatorChar;
        public static Encoding EncodingType;
        public static Localization.Layout CurrentLocalization;

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
                return "";

            String newStr = String.Empty;
            String[] tmp = str.Split(' ');

            for (int i = 0; i < tmp.Length; i++)
            {
                if (i >= num)
                    newStr += tmp[i] + ' ';
            }

            return newStr;
        }

        public static Player.Permission MatchPermission(String str)
        {
            String lookingFor = str.ToLower();

            for (Player.Permission Perm = Player.Permission.User; Perm < Player.Permission.Console; Perm++)
                if (lookingFor.Contains(Perm.ToString().ToLower())
                    || lookingFor.Contains(CurrentLocalization.LocalizationIndex[$"GLOBAL_PERMISSION_{Perm.ToString().ToUpper()}"].ToLower()))
                    return Perm;

            return Player.Permission.Banned;
        }

        /// <summary>
        /// Remove all IW Engine color codes
        /// </summary>
        /// <param name="str">String containing color codes</param>
        /// <returns></returns>
        public static String StripColors(this string str)
        {
            if (str == null)
                return "";
            str = Regex.Replace(str, @"(\^+((?![a-z]|[A-Z]).){0,1})+", "");
            string str2 = Regex.Match(str, @"(^\/+.*$)|(^.*\/+$)")
                .Value
                .Replace("/", " /");
            return str2.Length > 0 ? str2 : str;
        }

        /// <summary>
        /// Get the IW Engine color code corresponding to an admin level
        /// </summary>
        /// <param name="level">Specified player level</param>
        /// <returns></returns>
        public static String ConvertLevelToColor(Player.Permission level, string localizedLevel)
        {
            char colorCode = '6';
            // todo: maybe make this game independant?
            switch (level)
            {
                case Player.Permission.Banned:
                    colorCode = '1';
                    break;
                case Player.Permission.Flagged:
                    colorCode = '9';
                    break;
                case Player.Permission.Owner:
                    colorCode = '5';
                    break;
                case Player.Permission.User:
                    colorCode = '2';
                    break;
                case Player.Permission.Trusted:
                    colorCode = '3';
                    break;
                default:
                    break;
            }

            return $"^{colorCode}{localizedLevel ?? level.ToString()}";
        }

        public static string ToLocalizedLevelName(this Player.Permission perm) => CurrentLocalization.LocalizationIndex[$"GLOBAL_PERMISSION_{perm.ToString().ToUpper()}"];

        public static String ProcessMessageToken(this Server server, IList<Helpers.MessageToken> tokens, String str)
        {
            MatchCollection RegexMatches = Regex.Matches(str, @"\{\{[A-Z]+\}\}", RegexOptions.IgnoreCase);
            foreach (Match M in RegexMatches)
            {
                String Match = M.Value;
                String Identifier = M.Value.Substring(2, M.Length - 4);

                var found = tokens.FirstOrDefault(t => t.Name.ToLower() == Identifier.ToLower());

                if (found != null)
                    str = str.Replace(Match, found.Process(server));
            }

            return str;
        }

        public static bool IsBroadcastCommand(this string str)
        {
            return str[0] == '@';
        }

        /// <summary>
        /// Get the full gametype name
        /// </summary>
        /// <param name="input">Shorthand gametype reported from server</param>
        /// <returns></returns>
        public static String GetLocalizedGametype(String input)
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

        public static long ConvertLong(this string str)
        {
            str = str.Substring(0, Math.Min(str.Length, 16));
            if (Int64.TryParse(str, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out long id))
                return id;
            var bot = Regex.Match(str, @"bot[0-9]+").Value;
            if (!string.IsNullOrEmpty(bot))
                return -1;//Convert.ToInt64(bot.Substring(3)) + 1;
            return 0;
        }

        public static int ConvertToIP(this string str)
        {
            System.Net.IPAddress.TryParse(str, out System.Net.IPAddress ip);

            return ip == null ? 0 : BitConverter.ToInt32(ip.GetAddressBytes(), 0);
        }

        public static string ConvertIPtoString(this int ip)
        {
            return new System.Net.IPAddress(BitConverter.GetBytes(ip)).ToString();
        }

        public static String GetTimePassed(DateTime start)
        {
            return GetTimePassed(start, true);
        }

        public static String GetTimePassed(DateTime start, bool includeAgo)
        {
            TimeSpan Elapsed = DateTime.UtcNow - start;
            string ago = includeAgo ? $" {CurrentLocalization.LocalizationIndex["WEBFRONT_PENALTY_TEMPLATE_AGO"]}" : "";

            if (Elapsed.TotalSeconds < 30)
            {
                return CurrentLocalization.LocalizationIndex["GLOBAL_TIME_JUSTNOW"];
            }
            if (Elapsed.TotalMinutes < 120)
            {
                if (Elapsed.TotalMinutes < 1.5)
                    return $"1 {CurrentLocalization.LocalizationIndex["GLOBAL_TIME_MINUTES"]}{ago}";
                return Math.Round(Elapsed.TotalMinutes, 0) + $" {CurrentLocalization.LocalizationIndex["GLOBAL_TIME_MINUTES"]}{ago}";
            }
            if (Elapsed.TotalHours <= 24)
            {
                if (Elapsed.TotalHours < 1.5)
                    return $"1 {CurrentLocalization.LocalizationIndex["GLOBAL_TIME_HOURS"]}{ago}";
                return Math.Round(Elapsed.TotalHours, 0) + $" { CurrentLocalization.LocalizationIndex["GLOBAL_TIME_HOURS"]}{ago}";
            }
            if (Elapsed.TotalDays <= 90)
            {
                if (Elapsed.TotalDays < 1.5)
                    return $"1 {CurrentLocalization.LocalizationIndex["GLOBAL_TIME_DAYS"]}{ago}";
                return Math.Round(Elapsed.TotalDays, 0) + $" {CurrentLocalization.LocalizationIndex["GLOBAL_TIME_DAYS"]}{ago}";
            }
            if (Elapsed.TotalDays <= 365)
            {
                return $"{Math.Round(Elapsed.TotalDays / 7)} {CurrentLocalization.LocalizationIndex["GLOBAL_TIME_WEEKS"]}{ago}";
            }
            else
            {
                return $"{Math.Round(Elapsed.TotalDays / 30, 0)} {CurrentLocalization.LocalizationIndex["GLOBAL_TIME_MONTHS"]}{ago}";
            }
        }

        public static Game GetGame(string gameName)
        {
            if (gameName.Contains("IW4"))
                return Game.IW4;
            if (gameName.Contains("CoD4"))
                return Game.IW3;
            if (gameName.Contains("COD_WaW"))
                return Game.T4;
            if (gameName.Contains("COD_T5_S"))
                return Game.T5;
            if (gameName.Contains("T5M"))
                return Game.T5M;
            if (gameName.Contains("IW5"))
                return Game.IW5;
            if (gameName.Contains("COD_T6_S"))
                return Game.T6M;

            return Game.UKN;
        }

        public static string EscapeMarkdown(this string markdownString)
        {
            return markdownString.Replace("<", "\\<").Replace(">", "\\>").Replace("|", "\\|");
        }

        public static TimeSpan ParseTimespan(this string input)
        {
            var expressionMatch = Regex.Match(input, @"[0-9]+.\b");

            if (!expressionMatch.Success) // fallback to default tempban length of 1 hour
                return new TimeSpan(1, 0, 0);

            char lengthDenote = expressionMatch.Value.ToLower()[expressionMatch.Value.Length - 1];
            int length = Int32.Parse(expressionMatch.Value.Substring(0, expressionMatch.Value.Length - 1));

            var loc = CurrentLocalization.LocalizationIndex;

            if (lengthDenote == char.ToLower(loc["GLOBAL_TIME_MINUTES"][0]))
            {
                return new TimeSpan(0, length, 0);
            }

            if (lengthDenote == char.ToLower(loc["GLOBAL_TIME_HOURS"].First()))
            {
                return new TimeSpan(length, 0, 0);
            }

            if (lengthDenote == char.ToLower(loc["GLOBAL_TIME_DAYS"].First()))
            {
                return new TimeSpan(length, 0, 0, 0);
            }

            if (lengthDenote == char.ToLower(loc["GLOBAL_TIME_WEEKS"].First()))
            {
                return new TimeSpan(length * 7, 0, 0, 0);
            }

            if (lengthDenote == char.ToLower(loc["GLOBAL_TIME_YEARS"].First()))
            {
                return new TimeSpan(length * 365, 0, 0, 0);
            }

            return new TimeSpan(1, 0, 0);
        }

        public static string TimeSpanText(this TimeSpan span)
        {
            var loc = CurrentLocalization.LocalizationIndex;

            if (span.TotalMinutes < 60)
                return $"{span.Minutes} {loc["GLOBAL_TIME_MINUTES"]}";
            else if (span.Hours >= 1 && span.TotalHours < 24)
                return $"{span.Hours} {loc["GLOBAL_TIME_HOURS"]}";
            else if (span.TotalDays >= 1 && span.TotalDays < 7)
                return $"{span.Days} {loc["GLOBAL_TIME_DAYS"]}";
            else if (span.TotalDays >= 7 && span.TotalDays < 90)
                return $"{Math.Round(span.Days / 7.0, 0)} {loc["GLOBAL_TIME_WEEKS"]}";
            else if (span.TotalDays >= 90 && span.TotalDays < 365)
                return $"{Math.Round(span.Days / 30.0, 0)} {loc["GLOBAL_TIME_MONTHS"]}";
            else if (span.TotalDays >= 365 && span.TotalDays < 36500)
                return $"{Math.Round(span.Days / 365.0, 0)} {loc["GLOBAL_TIME_YEARS"]}";
            else if (span.TotalDays >= 36500)
                return loc["GLOBAL_TIME_FOREVER"];

            return "unknown";
        }

        public static Player AsPlayer(this Database.Models.EFClient client)
        {
            return client == null ? null : new Player()
            {
                Active = client.Active,
                AliasLink = client.AliasLink,
                AliasLinkId = client.AliasLinkId,
                ClientId = client.ClientId,
                ClientNumber = -1,
                FirstConnection = client.FirstConnection,
                Connections = client.Connections,
                NetworkId = client.NetworkId,
                TotalConnectionTime = client.TotalConnectionTime,
                Masked = client.Masked,
                Name = client.CurrentAlias.Name,
                IPAddress = client.CurrentAlias.IPAddress,
                Level = client.Level,
                LastConnection = client.LastConnection == DateTime.MinValue ? DateTime.UtcNow : client.LastConnection,
                CurrentAlias = client.CurrentAlias,
                CurrentAliasId = client.CurrentAlias.AliasId,
                // todo: make sure this is up to date
                IsBot = client.NetworkId == -1,
                Password = client.Password,
                PasswordSalt = client.PasswordSalt
            };
        }

        public static bool IsPrivileged(this Player p) => p.Level > Player.Permission.User;

        public static bool PromptBool(string question)
        {
            Console.Write($"{question}? [y/n]: ");
            return (Console.ReadLine().ToLower().FirstOrDefault() as char?) == 'y';
        }

        public static string PromptString(string question)
        {
            string response;
            do
            {
                Console.Write($"{question}: ");
                response = Console.ReadLine();
            } while (string.IsNullOrWhiteSpace(response));

            return response;
        }

        public static int ClientIdFromString(String[] lineSplit, int cIDPos)
        {
            int pID = -2; // apparently falling = -1 cID so i can't use it now
            int.TryParse(lineSplit[cIDPos].Trim(), out pID);

            if (pID == -1) // special case similar to mod_suicide
                int.TryParse(lineSplit[2], out pID);

            return pID;
        }

        public static Dictionary<string, string> DictionaryFromKeyValue(this string eventLine)
        {
            string[] values = eventLine.Substring(1).Split('\\');

            Dictionary<string, string> dict = null;

            if (values.Length % 2 == 0 && values.Length > 1)
            {
                dict = new Dictionary<string, string>();
                for (int i = 0; i < values.Length; i += 2)
                    dict.Add(values[i], values[i + 1]);
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

            return cmdLine.Length > 1 ? cmdLine[1] : cmdLine[0];
        }


        public static Task<Dvar<T>> GetDvarAsync<T>(this Server server, string dvarName) => server.RconParser.GetDvarAsync<T>(server.RemoteConnection, dvarName);

        public static Task SetDvarAsync(this Server server, string dvarName, object dvarValue) => server.RconParser.SetDvarAsync(server.RemoteConnection, dvarName, dvarValue);

        public static async Task<string[]> ExecuteCommandAsync(this Server server, string commandName) => await server.RconParser.ExecuteCommandAsync(server.RemoteConnection, commandName);

        public static Task<List<Player>> GetStatusAsync(this Server server) => server.RconParser.GetStatusAsync(server.RemoteConnection);

        public static async Task<Dictionary<string, string>> GetInfoAsync(this Server server)
        {
            var response = await server.RemoteConnection.SendQueryAsync(RCon.StaticHelpers.QueryType.GET_INFO);
            return response.FirstOrDefault(r => r[0] == '\\')?.DictionaryFromKeyValue();
        }

    }
}
