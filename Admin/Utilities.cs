using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;

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
            StringBuilder Cleaned = new StringBuilder();

            foreach (char c in S)
                if (c < 127 && c > 31 && c != 37 && c != 34 && c != 92) Cleaned.Append(c);
            return Cleaned.ToString();
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
                    return "^2" + Player.Permission.User;
                default:
                    return "^3" + level;
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

        public static String nameHTMLFormatted(Player P)
        {
            switch (P.getLevel())
            {
                case Player.Permission.User:
                    return "<span style='color:rgb(87, 150, 66)'>" + P.getName() + "</span>";
                case Player.Permission.Moderator:
                    return "<span style='color:#e7b402'>" + P.getName() + "</span>";
                case Player.Permission.Administrator:
                    return "<span style='color:#ec82de'>" + P.getName() + "</span>";
                case Player.Permission.SeniorAdmin:
                    return "<span style='color:#2eb6bf'>" + P.getName() + "</span>";
                case Player.Permission.Owner:
                    return "<span style='color:rgb(38,120,230)'>" + P.getName() + "</span>";
                case Player.Permission.Creator:
                    return "<span style='color:rgb(38,120,230)'>" + P.getName() + "</span>";
                case Player.Permission.Banned:
                    return "<span style='color:rgb(196, 22, 28)'>" + P.getName() + "</span>";
                case Player.Permission.Flagged:
                    return "<span style='color:rgb(251, 124, 98)'>" + P.getName() + "</span>";
                default:
                    return "<i>" + P.getName() + "</i>";
            }
        }

        public static String nameHTMLFormatted(Player.Permission Level)
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

        public static Dictionary<String, Player> playersFromStatus(String[] Status)
        {
            Dictionary<String, Player> playerDictionary = new Dictionary<String, Player>();

            if (Status == null) // looks like we didn't get a proper response
                return null;

            foreach (String S in Status)
            {
                String responseLine = S.Trim();

                if (Regex.Matches(responseLine, @"\d+$", RegexOptions.IgnoreCase).Count > 0 && responseLine.Length > 92) // its a client line!
                {
                    String[] playerInfo = responseLine.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                    int cID         = -1;
                    String cName    = stripColors(responseLine.Substring(46, 18)).Trim();
                    String npID     = responseLine.Substring(29, 17).Trim(); // DONT TOUCH PLZ
                    int.TryParse(playerInfo[0], out cID);
                    String cIP      = responseLine.Substring(72,20).Trim().Split(':')[0];

                    Player P        = new Player(cName, npID, cID, cIP);

                    try
                    {
                        playerDictionary.Add(npID, P);
                    }

                    catch(Exception E)
                    {
                        /// need to handle eventually
                        Console.WriteLine("Error handling player add -- " + E.Message);
                        continue;
                    }
                }
            }
            return playerDictionary;

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

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        public static dvar getDvar(int Location, int Handle)
        {
            int numberRead = 0;
            Byte[] Buff = new Byte[72];
            Byte[] Ptr = new Byte[4];

            ReadProcessMemory(Handle, Location, Ptr, Ptr.Length, ref numberRead); // get location of dvar

            ReadProcessMemory(Handle, (int)BitConverter.ToUInt32(Ptr, 0), Buff, Buff.Length, ref numberRead); // read dvar memory

            dvar_t dvar_raw = Helpers.ReadStruct<dvar_t>(Buff); // get the dvar struct

            dvar dvar_actual = new dvar(); // gotta convert to something readable

            dvar_actual.name = getStringFromPointer((int)dvar_raw.name, Handle);
            dvar_actual.description = getStringFromPointer((int)dvar_raw.description, Handle);

            if ((int)dvar_raw._default > short.MaxValue)
                dvar_actual._default = getStringFromPointer((int)dvar_raw._default, Handle);
            else
                dvar_actual._default = dvar_raw._default.ToString();

            if ((int)dvar_raw.current > short.MaxValue)
                dvar_actual.current = getStringFromPointer((int)dvar_raw.current, Handle);
            else
                dvar_actual.current = dvar_raw.current.ToString();

            if ((int)dvar_raw.latched > short.MaxValue)
                dvar_actual.latched = getStringFromPointer((int)dvar_raw.latched, Handle);
            else
                dvar_actual.latched = dvar_raw.latched.ToString();

            dvar_actual.type = dvar_raw.type;
            dvar_actual.flags = getIntFromPointer((int)dvar_raw.flags, Handle);
            dvar_actual.max = getIntFromPointer((int)dvar_raw.max, Handle);
            dvar_actual.min = getIntFromPointer((int)dvar_raw.min, Handle);

            // done!

            return dvar_actual;
        }

        public static String getStringFromPointer(int Location, int Handle)
        {
            int numberRead = 0;
            Byte[] Buff = new Byte[256];

            ReadProcessMemory(Handle, Location, Buff, Buff.Length, ref numberRead);

            StringBuilder str = new StringBuilder();
            for ( int i = 0; i < Buff.Length; i++)
            {
                if (Buff[i] == 0)
                    break;

                str.Append((char)Buff[i]);
            }
            return str.ToString();
        }

        public static int getIntFromPointer(int Location, int Handle)
        {
            int numberRead = 0;
            Byte[] Buff = new Byte[4];

            ReadProcessMemory(Handle, Location, Buff, Buff.Length, ref numberRead);

            return BitConverter.ToInt32(Buff, 0);
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
