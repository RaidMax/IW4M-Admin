using System;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace SharedLibrary
{
    public class Utilities
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
        public static String removeWords(String str, int num)
        {
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

            for (Player.Permission Perm = Player.Permission.User; Perm < Player.Permission.Owner; Perm++)
            {
                if (lookingFor.Contains(Perm.ToString().ToLower()))
                    return Perm;
            }

            return Player.Permission.Banned;
        }

        public static String removeNastyChars(String str)
        {
            if (str != null)
                return str.Replace("`", "").Replace("\\", "").Replace("\"", "").Replace("&quot;", "''").Replace("&amp;", "&").Replace("\"", "''");
            else
                return String.Empty;
        }

        public static int GetLineNumber(Exception ex)
        {
            var lineNumber = 0;
            const string lineSearch = ":line ";
            var index = ex.StackTrace.LastIndexOf(lineSearch);
            if (index != -1)
            {
                var lineNumberText = ex.StackTrace.Substring(index + lineSearch.Length);
                if (int.TryParse(lineNumberText, out lineNumber))
                {
                }
            }
            return lineNumber;
        }

        public static String cleanChars(String S)
        {
            if (S == null)
                return "";

            StringBuilder Cleaned = new StringBuilder();

            foreach (char c in S)
                if (c < 127 && c > 31 && c != 37 && c != 34 && c != 92) Cleaned.Append(c);
            return Cleaned.ToString();
        }

        public static String stripColors(String str)
        {
            if (str == null)
                return "";
            return Regex.Replace(str, @"\^[0-9]", "");
        }

        public static String levelToColor(Player.Permission level)
        {
            switch (level)
            {
                case Player.Permission.Banned:
                    return "^1" + Player.Permission.Banned;
                case Player.Permission.Flagged:
                    return "^0" + Player.Permission.Flagged;
                case Player.Permission.Owner:
                    return "^5" + Player.Permission.Owner;
                case Player.Permission.User:
                    return "^2" + Player.Permission.User;
                default:
                    return "^3" + level;
            }
        }

        public static String levelHTMLFormatted(Player.Permission Level)
        {
            switch (Level)
            {
                case Player.Permission.User:
                    return "<span style='color:rgb(87, 150, 66)'>" + Level + "</span>";
                case Player.Permission.Moderator:
                    return "<span style='color:#e7b402'>" + Level + "</span>";
                case Player.Permission.Administrator:
                    return "<span style='color:#ec82de'>" + Level + "</span>";
                case Player.Permission.SeniorAdmin:
                    return "<span style='color:#2eb6bf'>" + Level + "</span>";
                case Player.Permission.Owner:
                    return "<span style='color:rgb(38,120,230)'>" + Level + "</span>";
                case Player.Permission.Creator:
                    return "<span style='color:rgb(38,120,230)'>" + Level + "</span>";
                case Player.Permission.Banned:
                    return "<span style='color:rgb(196, 22, 28)'>" + Level + "</span>";
                case Player.Permission.Flagged:
                    return "<span style='color:rgb(251, 124, 98)'>" + Level + "</span>";
                default:
                    return "<i>" + Level + "</i>";
            }
        }

        public static String nameHTMLFormatted(Player P)
        {
            switch (P.Level)
            {
                case Player.Permission.User:
                    return "<span style='color:rgb(87, 150, 66)'>" + P.Name + "</span>";
                case Player.Permission.Moderator:
                    return "<span style='color:#e7b402'>" + P.Name + "</span>";
                case Player.Permission.Administrator:
                    return "<span style='color:#ec82de'>" + P.Name + "</span>";
                case Player.Permission.SeniorAdmin:
                    return "<span style='color:#2eb6bf'>" + P.Name + "</span>";
                case Player.Permission.Owner:
                    return "<span style='color:rgb(38,120,230)'>" + P.Name + "</span>";
                case Player.Permission.Creator:
                    return "<span style='color:rgb(38,120,230)'>" + P.Name + "</span>";
                case Player.Permission.Banned:
                    return "<span style='color:rgb(196, 22, 28)'>" + P.Name + "</span>";
                case Player.Permission.Flagged:
                    return "<span style='color:rgb(251, 124, 98)'>" + P.Name + "</span>";
                default:
                    return "<i>" + P.Name + "</i>";
            }
        }

        public static String processMacro(Dictionary<String, Object> Dict, String str)
        {
            MatchCollection Found = Regex.Matches(str, @"\{\{[A-Z]+\}\}", RegexOptions.IgnoreCase);
            foreach (Match M in Found)
            {
                String Match = M.Value;
                String Identifier = M.Value.Substring(2, M.Length - 4);
                Object foundVal;
                Dict.TryGetValue(Identifier, out foundVal);
                String Replacement;

                if (foundVal != null)
                    Replacement = foundVal.ToString();
                else
                    Replacement = "";

                str = str.Replace(Match, Replacement);
            }

            return str;
        }

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
                    return "Unknown";
            }
        }

        public static String DateTimeSQLite(DateTime datetime)
        {
            string dateTimeFormat = "{0}-{1}-{2} {3}:{4}:{5}.{6}";
            return string.Format(dateTimeFormat, datetime.Year, datetime.Month, datetime.Day, datetime.Hour, datetime.Minute, datetime.Second, datetime.Millisecond);
        }

        public static String timePassed(DateTime start)
        {
            TimeSpan Elapsed = DateTime.Now - start;

            if (Elapsed.TotalMinutes < 120)
                return Math.Round(Elapsed.TotalMinutes, 0) + " minutes";
            if (Elapsed.TotalHours <= 24)
                return Math.Round(Elapsed.TotalHours, 0) + " hours";
            if (Elapsed.TotalDays <= 365)
                return Math.Round(Elapsed.TotalDays, 0) + " days";
            else
                return "a very long time";
        }

        public static String timesConnected(int connection)
        {
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
                default:
                    return connection.ToString() + Prefix;
            }
        }
    }
}
