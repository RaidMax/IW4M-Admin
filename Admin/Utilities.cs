using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;

namespace IW4MAdmin
{
    class Utilities
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
            Thread.Sleep((int)Math.Ceiling(time*1000));
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
                return str.Replace("`", "").Replace("\\", "").Replace("\"", "").Replace("^", "").Replace("&quot;", "''").Replace("&amp;", "&").Replace("\"", "''");
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

        public static String stripColors(String str)
        {
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
                    return "^3" + Player.Permission.User;
                default:
                    return "^2" + level;
            }
        }

        public static String processMacro(Dictionary<String, Object> Dict, String str)
        {
            MatchCollection Found = Regex.Matches(str, @"\{\{[A-Z]+\}\}", RegexOptions.IgnoreCase);
            foreach (Match M in Found)
            {
                String Match = M.Value;
                String Identifier = M.Value.Substring(2, M.Length - 4);
                String Replacement = Dict[Identifier].ToString();
                str = str.Replace(Match, Replacement);
            }

            return str;
        }

        public static Dictionary<String, String> IPFromStatus(String[] players)
        {
            Dictionary<String, String> Dict = new Dictionary<String, String>();

            if (players == null)
                return null;

            foreach (String S in players)
            {
                String S2 = S.Trim();
                if (S.Length < 50)
                    continue;
                if (Regex.Matches(S2, @"\d+$", RegexOptions.IgnoreCase).Count > 0)
                {
                    String[] eachPlayer = S2.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 3; i < eachPlayer.Length; i++ )
                    {
                        if (eachPlayer[i].Split(':').Length > 1)
                        {
                            Dict.Add(eachPlayer[3], eachPlayer[i].Split(':')[0]);
                            break;
                        }
                    }
                        
                }
           } 
            return Dict;
            
        }

        public static String DateTimeSQLite(DateTime datetime)
        {
            string dateTimeFormat = "{0}-{1}-{2} {3}:{4}:{5}.{6}";
            return string.Format(dateTimeFormat, datetime.Year, datetime.Month, datetime.Day, datetime.Hour, datetime.Minute, datetime.Second, datetime.Millisecond);
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
