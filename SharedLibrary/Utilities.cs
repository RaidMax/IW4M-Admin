using System;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace SharedLibrary
{
    public static class Utilities
    {
        //Get string with specified number of spaces -- really only for visual output
        public static String getSpaces(int Num)
        {
            String SpaceString = String.Empty;
            while (Num > 0)
            {
                SpaceString += ' ';
                Num--;
            }

            return SpaceString;
        }

        //Sleep for x amount of seconds
        public static void Wait(double time)
        {
            Thread.Sleep((int)Math.Ceiling(time * 1000));
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

        public static Player.Permission matchPermission(String str)
        {
            String lookingFor = str.ToLower();
#if REPZ_BUILD
            for (Player.Permission Perm = Player.Permission.User; Perm <= Player.Permission.Owner; Perm++)
#else
            for (Player.Permission Perm = Player.Permission.User; Perm < Player.Permission.Owner; Perm++)
#endif
            {
                if (lookingFor.Contains(Perm.ToString().ToLower()))
                    return Perm;
            }

            return Player.Permission.Banned;
        }

        public static String removeNastyChars(String str)
        {
            if (str != null)
            {
                return str.Replace("`", "").Replace("\\", "").Replace("\"", "").Replace("&quot;", "").Replace("&amp;", "&").Replace("\"", "''").Replace("'", "").Replace("?", "");
            }

            else
                return String.Empty;
        }

        public static String CleanChars(this string S)
        {
            if (S == null)
                return "";

            StringBuilder Cleaned = new StringBuilder();

            foreach (char c in S)
                if (c < 127 && c > 31 && c != 37 && c != 34 && c != 92) Cleaned.Append(c);
            return Cleaned.ToString();
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
            return Regex.Replace(str, @"\^([0-9]|\:)", "");
        }

        /// <summary>
        /// Get the IW Engine color code corresponding to an admin level
        /// </summary>
        /// <param name="level">Specified player level</param>
        /// <returns></returns>
        public static String levelToColor(Player.Permission level)
        {
            switch (level)
            {
                case Player.Permission.Banned:
                    return "^1" + Player.Permission.Banned;
                case Player.Permission.Flagged:
                    return "^9" + Player.Permission.Flagged;
                case Player.Permission.Owner:
                    return "^5" + Player.Permission.Owner;
                case Player.Permission.User:
                    return "^2" + Player.Permission.User;
                case Player.Permission.Trusted:
                    return "^3" + Player.Permission.Trusted;
                default:
                    return "^6" + level;
            }
        }

        public static String ProcessMessageToken(IList<MessageToken> tokens, String str)
        {
            MatchCollection RegexMatches = Regex.Matches(str, @"\{\{[A-Z]+\}\}", RegexOptions.IgnoreCase);
            foreach (Match M in RegexMatches)
            {
                String Match = M.Value;
                String Identifier = M.Value.Substring(2, M.Length - 4);

                var found = tokens.FirstOrDefault(t => t.Name.ToLower() == Identifier.ToLower());

                if (found != null)
                    str = str.Replace(Match, found.ToString());
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
        public static String gametypeLocalized(String input)
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

        public static String DateTimeSQLite(DateTime datetime)
        {
            return datetime.ToString("yyyy-MM-dd H:mm:ss");
        }

        public static String timePassed(DateTime start)
        {
            TimeSpan Elapsed = DateTime.Now - start;

            if (Elapsed.TotalSeconds < 30)
                return "just now";
            if (Elapsed.TotalMinutes < 120)
            {
                if (Elapsed.TotalMinutes < 1.5)
                    return "1 minute";
                return Math.Round(Elapsed.TotalMinutes, 0) + " minutes";
            }
            if (Elapsed.TotalHours <= 24)
            {
                if (Elapsed.TotalHours < 1.5)
                    return "1 hour";
                return Math.Round(Elapsed.TotalHours, 0) + " hours";
            }
            if (Elapsed.TotalDays <= 365)
            {
                if (Elapsed.TotalDays  < 1.5)
                    return "1 day";
                return Math.Round(Elapsed.TotalDays, 0) + " days";
            }
            else
                return "a very long time";
        }

        public static String TimesConnected(this Player P)
        {
            int connection = P.Connections;
            String Prefix = String.Empty;
            if (connection % 10 > 3 || connection % 10 == 0 || (connection % 100 > 9 && connection % 100 < 19))
                Prefix = "th";
            else
            {
                switch (connection % 10)
                {
                    case 1:
                        Prefix = "st";
                        break;
                    case 2:
                        Prefix = "nd";
                        break;
                    case 3:
                        Prefix = "rd";
                        break;
                }
            }

            switch (connection)
            {
                case 0:
                case 1:
                    return "first";
                case 2:
                    return "second";
                case 3:
                    return "third";
                case 4:
                    return "fourth";
                case 5:
                    return "fifth";
                case 100:
                    return "One-Hundreth (amazing!)";
                case 500:
                    return "you're really ^5dedicated ^7to this server! This is your ^5500th^7";
                case 1000:
                    return "you deserve a medal. it's your ^11000th^7";

                default:
                    return connection.ToString() + Prefix;
            }
        }
    }
}
