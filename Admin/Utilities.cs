using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.IO;

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

        [Flags]
        public enum ProcessAccessFlags : uint
        {
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VMOperation = 0x00000008,
            VMRead = 0x00000010,
            VMWrite = 0x00000020,
            DupHandle = 0x00000040,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            Synchronize = 0x00100000,
            All = 0x001F0FFF
        }

        [Flags]
        public enum AllocationType
        {
            Commit = 0x00001000,
            Reserve = 0x00002000,
            Decommit = 0x00004000,
            Release = 0x00008000,
            Reset = 0x00080000,
            TopDown = 0x00100000,
            WriteWatch = 0x00200000,
            Physical = 0x00400000,
            LargePages = 0x20000000
        }

        [Flags]
        public enum MemoryProtection
        {
            NoAccess = 0x0001,
            ReadOnly = 0x0002,
            ReadWrite = 0x0004,
            WriteCopy = 0x0008,
            Execute = 0x0010,
            ExecuteRead = 0x0020,
            ExecuteReadWrite = 0x0040,
            ExecuteWriteCopy = 0x0080,
            GuardModifierflag = 0x0100,
            NoCacheModifierflag = 0x0200,
            WriteCombineModifierflag = 0x0400
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern bool TerminateThread(IntPtr hThread, uint dwExitCode);

        [DllImport("kernel32.dll")]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [DllImport("kernel32.dll")]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int CloseHandle(IntPtr hObject);

        public static dvar getDvar(int Location, IntPtr Handle)
        {
            int numberRead = 0;
            Byte[] Buff = new Byte[72];

            ReadProcessMemory((int)Handle, Location, Buff, Buff.Length, ref numberRead); // read dvar memory

            dvar_t dvar_raw = Helpers.ReadStruct<dvar_t>(Buff); // get the dvar struct
            dvar dvar_actual = new dvar(); // gotta convert to something readable

            dvar_actual.name = getStringFromPointer((int)dvar_raw.name, (int)Handle);
            dvar_actual.description = getStringFromPointer((int)dvar_raw.description, (int)Handle);

            if ((int)dvar_raw._default > short.MaxValue)
                dvar_actual._default = getStringFromPointer((int)dvar_raw._default, (int)Handle);
            else
                dvar_actual._default = dvar_raw._default.ToString();

            if ((int)dvar_raw.current > short.MaxValue)
                dvar_actual.current = getStringFromPointer((int)dvar_raw.current, (int)Handle);
            else if ((int)dvar_raw.current <= 1025)
                dvar_actual.current = ((int)dvar_raw.current % 1024).ToString();
            else
                dvar_actual.current = dvar_raw.current.ToString();

            if ((int)dvar_raw.latched > short.MaxValue)
                dvar_actual.latched = getStringFromPointer((int)dvar_raw.latched, (int)Handle);
            else
                dvar_actual.latched = dvar_raw.latched.ToString();

            dvar_actual.type = dvar_raw.type;
            dvar_actual.flags = getIntFromPointer((int)dvar_raw.flags, (int)Handle);
            dvar_actual.max = getIntFromPointer((int)dvar_raw.max, (int)Handle);
            dvar_actual.min = getIntFromPointer((int)dvar_raw.min, (int)Handle);

            // done!

            return dvar_actual;
        }

        public static dvar getDvarOld(int Location, int Handle)
        {
            int loc = getIntFromPointer(Location, Handle);
            return getDvar(loc, (IntPtr)Handle);
        }

        public static int getDvarCurrentAddress(int Location, int Handle)
        {
            int numberRead = 0;
            Byte[] Buff = new Byte[72];
            Byte[] Ptr = new Byte[4];

            ReadProcessMemory(Handle, Location, Ptr, Ptr.Length, ref numberRead); // get location of dvar
            ReadProcessMemory(Handle, (int)BitConverter.ToUInt32(Ptr, 0), Buff, Buff.Length, ref numberRead); // read dvar memory

            dvar_t dvar_raw = Helpers.ReadStruct<dvar_t>(Buff); // get the dvar struct
            int current = (int)dvar_raw.current;

            return current;
        }

        public static void setDvar(int Location, int Handle, String Value)
        {
            UIntPtr bytesWritten = UIntPtr.Zero;
            WriteProcessMemory((IntPtr)Handle, (IntPtr)Location, Encoding.ASCII.GetBytes(Value), (uint)Value.Length, out bytesWritten);
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

        public static Boolean getBoolFromPointer(int Location, int Handle)
        {
            int numberRead = 0;
            Byte[] Buff = new Byte[1];

            ReadProcessMemory(Handle, Location, Buff, Buff.Length, ref numberRead);

            return BitConverter.ToBoolean(Buff, 0);
        }

        public static void executeCommand(int pID, String Command)
        {
            /*IntPtr ProcessHandle = OpenProcess(ProcessAccessFlags.All, false, pID);
            IntPtr memoryForCMDName = allocateAndWrite(Encoding.ASCII.GetBytes(Command + "\0"), ProcessHandle);
            uint threadID;

            if (memoryForCMDName == IntPtr.Zero)  
                return;

            // set the dvar's current value pointer to our desired command
            setDvarCurrentPtr((IntPtr)0x2098D9C, memoryForCMDName, ProcessHandle);

            // assembly instruction to execute the command we want stored in `surogate` dvar
            byte[] executeCMD = {     
                                    0x55, 0x8B, 0xEC, 0x51, 0xC7, 0x45, 0xFC,   // ---------------------------------------------
                                    0x9C, 0x8D, 0x09, 0x02, 0x8B, 0x45, 0xFC,   // dvar_t** dvarWithCMD = (dvar_t**)(0x2098D9C);
                                    0x8B, 0x08, 0x8B, 0x51, 0x10, 0x52, 0x6A, 
                                    0x00, 0x6A, 0x00, 0xFF, 0x15, 0x1C, 0x53,   // Cmd_ExecuteSingleCommand(0, 0, (*dvarWithCMD)->current.string );
                                    0x11, 0x10, 0x83, 0xC4, 0x0C, 0x8B, 0xE5,   // ---------------------------------------------
                                    0x5D, 0xC3
                                };
            
            // allocate the memory for the assembly command and write it
            IntPtr codeAllocation = allocateAndWrite(executeCMD, ProcessHandle);
            if (codeAllocation == IntPtr.Zero)
                return;

            // create our thread that executes command :)
            IntPtr ThreadHandle = CreateRemoteThread(ProcessHandle, IntPtr.Zero, 0, codeAllocation, IntPtr.Zero, 0, out threadID);
            if (ThreadHandle == null || ThreadHandle == IntPtr.Zero)
                return;

            WaitForSingleObject(ThreadHandle, Int32.MaxValue); // gg if it doesn't finishe

            // cleanup
            if (!VirtualFreeEx(ProcessHandle, codeAllocation, 0, AllocationType.Release))
                Console.WriteLine(Marshal.GetLastWin32Error());
            if (!VirtualFreeEx(ProcessHandle, memoryForCMDName, 0, AllocationType.Release))
                Console.WriteLine(Marshal.GetLastWin32Error());*/

            IntPtr ProcessHandle = OpenProcess(ProcessAccessFlags.All, false, pID);
            IntPtr memoryForDvarName = allocateAndWrite(Encoding.ASCII.GetBytes(Command +  "\0"), ProcessHandle);

            if (memoryForDvarName == IntPtr.Zero)
            {
                Console.WriteLine("UNABLE TO ALLOCATE MEMORY FOR DVAR NAME");
                return;
            }

            setDvarCurrentPtr(0x2098D9C, memoryForDvarName, ProcessHandle);

          //  if (!VirtualFreeEx(ProcessHandle, memoryForDvarName, 0, AllocationType.Release))
            //    Console.WriteLine("Virtual Free Failed -- Error #" + Marshal.GetLastWin32Error());

            CloseHandle(ProcessHandle);

        }

        public static IntPtr allocateAndWrite(Byte[] Data, IntPtr ProcessHandle)
        {
            UIntPtr bytesWritten;
            IntPtr AllocatedMemory = VirtualAllocEx(ProcessHandle, IntPtr.Zero, (uint)Data.Length, AllocationType.Commit, MemoryProtection.ExecuteReadWrite);
            if (!WriteProcessMemory(ProcessHandle, AllocatedMemory, Data, (uint)Data.Length, out bytesWritten))
            {
                Console.WriteLine("UNABLE TO WRITE PROCESS MEMORY!");
                return IntPtr.Zero;
            }

            if ((int)bytesWritten != Data.Length)
                return IntPtr.Zero;
            else
                return AllocatedMemory;
        }

        public static bool setDvarCurrentPtr(int DvarAddress, IntPtr ValueAddress, IntPtr ProcessHandle)
        {
            int locationOfCurrentPtr = getIntFromPointer(DvarAddress, (int)ProcessHandle) + 0x10;
            Byte[] newTextPtr = BitConverter.GetBytes((int)ValueAddress);
            UIntPtr bytesWritten;
            if (!WriteProcessMemory(ProcessHandle, (IntPtr)locationOfCurrentPtr, newTextPtr, (uint)newTextPtr.Length, out bytesWritten))
                return false;
            if (newTextPtr.Length != (int)bytesWritten)
                return false;
            return true;
        }

        public static bool initalizeInterface(int pID)
        {
            String Path = AppDomain.CurrentDomain.BaseDirectory + "lib\\AdminInterface.dll";

            if (!File.Exists(Path))
                return false;

            UIntPtr bytesWritten;
            uint threadID;

            IntPtr ProcessHandle = OpenProcess(ProcessAccessFlags.All, false, pID);
            if (ProcessHandle == IntPtr.Zero)
                return false;

            IntPtr lpLLAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");

            if (lpLLAddress == IntPtr.Zero)
                return false;

            IntPtr pathAllocation = VirtualAllocEx(ProcessHandle, IntPtr.Zero, (uint)Path.Length + 1, AllocationType.Commit, MemoryProtection.ExecuteReadWrite);

            if (pathAllocation == IntPtr.Zero)
                return false;

            byte[] pathBytes = Encoding.ASCII.GetBytes(Path);

            if (!WriteProcessMemory(ProcessHandle, pathAllocation, pathBytes, (uint)pathBytes.Length, out bytesWritten))
                return false;

            if (CreateRemoteThread(ProcessHandle, IntPtr.Zero, 0, lpLLAddress, pathAllocation, 0, out threadID) == IntPtr.Zero)
                return false;

            CloseHandle(ProcessHandle);

            return true;
        }

        public static void setDvar(int pID, String Name, String Value)
        {
           /* IntPtr ProcessHandle = OpenProcess(ProcessAccessFlags.All, false, pID);
            IntPtr memoryForDvarName = allocateAndWrite(Encoding.ASCII.GetBytes(Name + " " + Value + "\0"), ProcessHandle);

            if (memoryForDvarName == IntPtr.Zero)
            {
                Console.WriteLine("UNABLE TO ALLOCATE MEMORY FOR DVAR NAME");
                return;
            }

            setDvarCurrentPtr(0x2098D9C, memoryForDvarName, ProcessHandle);

            if (!VirtualFreeEx(ProcessHandle, memoryForDvarName, 0, AllocationType.Release))
                Console.WriteLine("Virtual Free Failed -- Error #" + Marshal.GetLastWin32Error());

            CloseHandle(ProcessHandle);*/
        }

        public static dvar getDvar(int pID, String DVAR)
        {
            dvar requestedDvar =        new dvar();
            IntPtr ProcessHandle =      OpenProcess(ProcessAccessFlags.All, false, pID);
            IntPtr memoryForDvarName =  allocateAndWrite(Encoding.ASCII.GetBytes(DVAR + "\0"), ProcessHandle);

            if (memoryForDvarName == IntPtr.Zero)
            {
                Console.WriteLine("UNABLE TO ALLOCATE MEMORY FOR DVAR NAME");
                return requestedDvar;
            }

            setDvarCurrentPtr(0x2089E04, memoryForDvarName, ProcessHandle); // sv_debugRate
#if ASD
           /* byte[] copyDvarValue = {   
                                    0x55, 0x8B, 0xEC, 0x83, 0xEC, 0x08, // -----------------------------------------------
                                    0xC7, 0x45, 0xFC, 0x9C, 0x8D, 0x09, // dvar_t** surogateDvar = (dvar_t**)(0x2098D9C);
                                    0x02, 0x8B, 0x45, 0xFC, 0x8B, 0x08, //
                                    0x8B, 0x51, 0x10, 0x52, 0xFF, 0x15,	// dvar_t *newDvar = Dvar_FindVar((*surogateDvar)->current.string);
                                    0x6C, 0x53, 0x11, 0x10, 0x83, 0xC4, // 
                                    0x04, 0x89, 0x45, 0xF8, 0x83, 0x7D, // if (newDvar)
                                    0xF8, 0x00, 0x74, 0x0B, 0x8B, 0x45, // 
                                    0xFC, 0x8B, 0x08, 0x8B, 0x55, 0xF8, // (*surogateDvar)->current.integer = (int)newDvar;
                                    0x89, 0x51, 0x10, 0x8B, 0xE5, 0x5D, // -----------------------------------------------
                                    0xC3
                                };
          
            IntPtr codeAllocation = allocateAndWrite(copyDvarValue, ProcessHandle);

            if (codeAllocation == IntPtr.Zero)
                Console.WriteLine("UNABLE TO ALLOCATE MEMORY FOR CODE");
                                                                  
            IntPtr ThreadHandle = CreateRemoteThread(ProcessHandle, IntPtr.Zero, 0, codeAllocation, IntPtr.Zero, 0, out threadID);
            if (ThreadHandle == null || ThreadHandle == IntPtr.Zero)
                return requestedDvar;

            WaitForSingleObject(ThreadHandle, Int32.MaxValue); //  gg if thread doesn't finish

            if (!VirtualFreeEx(ProcessHandle, codeAllocation, 0, AllocationType.Release))
                Console.WriteLine(Marshal.GetLastWin32Error());
            if (!VirtualFreeEx(ProcessHandle, memoryForDvarName, 0, AllocationType.Release))
                Console.WriteLine(Marshal.GetLastWin32Error());*/
#endif
            Utilities.Wait(.3);
            int dvarLoc = getIntFromPointer(0x2089E04, (int)ProcessHandle); // this is where the dvar is stored

            if (dvarLoc == 0)
                return requestedDvar;

            dvarLoc = getIntFromPointer(dvarLoc + 0x10, (int)ProcessHandle);

            requestedDvar = getDvar(dvarLoc, ProcessHandle);
            CloseHandle(ProcessHandle);

            return requestedDvar;
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
