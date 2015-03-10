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
            return str.Replace("`", "").Replace("'", "").Replace("\\", "").Replace("\"", "").Replace("^", "").Replace("&quot;", "''");
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
    }
}
