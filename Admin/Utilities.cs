using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.IO;
using SharedLibrary;

namespace IW4MAdmin
{
    class Utilities
    {
        const int PROCESS_CREATE_THREAD = 0x0002;
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int PROCESS_VM_OPERATION = 0x0008;
        const int PROCESS_VM_WRITE = 0x0020;
        const int PROCESS_VM_READ = 0x0010;

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
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern bool TerminateThread(IntPtr hThread, uint dwExitCode);

        [DllImport("kernel32.dll")]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType flAllocationType, MemoryProtection flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out uint lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, AllocationType dwFreeType);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern UInt32 WaitForSingleObject(IntPtr hHandle, UInt32 dwMilliseconds);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern int CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        static extern bool GetExitCodeThread(IntPtr hThread, out uint lpExitCode);

        [DllImport("ntdll.dll")]
        public static extern uint RtlCreateUserThread(
            [In] IntPtr Process,
            [In] IntPtr ThreadSecurityDescriptor,
            [In] bool CreateSuspended,
            [In] int StackZeroBits,
            uint MaximumStackSize,
            [In] [Optional] IntPtr InitialStackSize,
            [In] IntPtr StartAddress,
            [In] IntPtr Parameter,
            [Out] out IntPtr Thread,
            [Out] out ClientId ClientId
        );

        [StructLayout(LayoutKind.Sequential)]
        public struct ClientId
        {
            public ClientId(int processId, int threadId)
            {
                this.UniqueProcess = new IntPtr(processId);
                this.UniqueThread = new IntPtr(threadId);
            }

            public IntPtr UniqueProcess;
            public IntPtr UniqueThread;

            public int ProcessId { get { return this.UniqueProcess.ToInt32(); } }
            public int ThreadId { get { return this.UniqueThread.ToInt32(); } }
        }

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
            for (int i = 0; i < Buff.Length; i++)
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

        public static IntPtr executeCommand(int pID, String Command, IntPtr lastMemoryLocation)
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
                Program.getManager().mainLog.Write(Marshal.GetLastWin32Error());
            if (!VirtualFreeEx(ProcessHandle, memoryForCMDName, 0, AllocationType.Release))
                Program.getManager().mainLog.Write(Marshal.GetLastWin32Error());*/

            IntPtr ProcessHandle = OpenProcess(ProcessAccessFlags.All, false, pID);


            if (lastMemoryLocation != IntPtr.Zero)
            {
                if (!VirtualFreeEx(ProcessHandle, lastMemoryLocation, 0, AllocationType.Release))
                {
                    Program.getManager().mainLog.Write("Virtual Free Failed -- Error #" + Marshal.GetLastWin32Error());
                    return IntPtr.Zero;
                }
            }

            IntPtr memoryForDvarName = allocateAndWrite(Encoding.ASCII.GetBytes(Command + '\0'), ProcessHandle); // this gets disposed next call

            if (memoryForDvarName == IntPtr.Zero)
            {
                Program.getManager().mainLog.Write("UNABLE TO ALLOCATE MEMORY FOR DVAR NAME");
                return IntPtr.Zero;
            }

            setDvarCurrentPtr(0x2098D9C, memoryForDvarName, ProcessHandle);
            CloseHandle(ProcessHandle);

            return memoryForDvarName;
        }

        public static IntPtr allocateAndWrite(Byte[] Data, IntPtr ProcessHandle)
        {
            UIntPtr bytesWritten;
            IntPtr AllocatedMemory = VirtualAllocEx(ProcessHandle, IntPtr.Zero, (uint)Data.Length, AllocationType.Commit, MemoryProtection.ExecuteReadWrite);
            if (!WriteProcessMemory(ProcessHandle, AllocatedMemory, Data, (uint)Data.Length, out bytesWritten))
            {
                Program.getManager().mainLog.Write("Unable to write process memory!");
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

        public static bool shutdownInterface(int pID, params IntPtr[] cleanUp)
        {
            IntPtr threadID;
            IntPtr ProcessHandle = OpenProcess(ProcessAccessFlags.All, false, pID);
#if DEBUG
            Program.getManager().mainLog.Write("Process handle is: " + ProcessHandle);
#endif
            if (ProcessHandle == IntPtr.Zero)
            {
                Program.getManager().mainLog.Write("Unable to open target process");
                return false;
            }

            List<IntPtr> baseAddresses = new List<IntPtr>();

            System.Diagnostics.Process P = System.Diagnostics.Process.GetProcessById(pID);
            foreach (System.Diagnostics.ProcessModule M in P.Modules)
            {
                if (M.ModuleName == "AdminInterface.dll" && M.BaseAddress != IntPtr.Zero)
                    baseAddresses.Add(M.BaseAddress);
            }

            IntPtr lpLLAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "FreeLibraryAndExitThread");

            if (lpLLAddress == IntPtr.Zero)
            {
                Program.getManager().mainLog.Write("Could not obtain address of freelibary");
                return false;
            }

            ClientId clientid = new ClientId();
            threadID = new IntPtr();

            foreach (IntPtr baseAddress in baseAddresses)
            {
                RtlCreateUserThread(ProcessHandle, IntPtr.Zero, false, 0, (uint)0, IntPtr.Zero, lpLLAddress, baseAddress, out threadID, out clientid);
                if (threadID == IntPtr.Zero)
                {
                    Program.getManager().mainLog.Write("Could not create remote thread");
                    return false;
                }
#if DEBUG
            Program.getManager().mainLog.Write("Thread ID is " + threadID);
#endif
                uint responseCode = WaitForSingleObject(threadID, 3000);

                if (responseCode != 0x00000000L)
                {
                    Program.getManager().mainLog.Write("Thread did not finish in a timely manner!", Log.Level.Debug);
                    Program.getManager().mainLog.Write("Last error is: " + Marshal.GetLastWin32Error(), Log.Level.Debug);
                    return false;
                }
            }

            CloseHandle(ProcessHandle);

            foreach (IntPtr Pointer in cleanUp)
            {
                if (Pointer != IntPtr.Zero)
                {
                      if (!VirtualFreeEx(ProcessHandle, Pointer, 0, AllocationType.Release))
                         Program.getManager().mainLog.Write("Virtual Free Failed During Exit Cleanup -- Error #" + Marshal.GetLastWin32Error());
                }
            }

#if DEBUG
            Program.getManager().mainLog.Write("Shutdown finished -- last error : " + Marshal.GetLastWin32Error());
#endif
            return true;
        }
        /////////////////////////////////////////////////////////////

        public static Boolean initalizeInterface(int pID)
        {
            String Path = AppDomain.CurrentDomain.BaseDirectory + "lib\\AdminInterface.dll";

            if (!File.Exists(Path))
            {
                Program.getManager().mainLog.Write("AdminInterface DLL does not exist!");
                return false;
            }

            UIntPtr bytesWritten;
            IntPtr threadID;

            IntPtr ProcessHandle = OpenProcess(ProcessAccessFlags.All, false, pID);
#if DEBUG
            Program.getManager().mainLog.Write("Process handle is: " + ProcessHandle);
#endif
            if (ProcessHandle == IntPtr.Zero)
            {
                Program.getManager().mainLog.Write("Unable to open target process");
                return false;
            }

            IntPtr lpLLAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");

            if (lpLLAddress == IntPtr.Zero)
            {
                Program.getManager().mainLog.Write("Could not obtain address of function address");
                return false;
            }

#if DEBUG
            Program.getManager().mainLog.Write("LoadLibraryA location is 0x" + lpLLAddress.ToString("X8"));
#endif

            IntPtr pathAllocation = VirtualAllocEx(ProcessHandle, IntPtr.Zero, (uint)Path.Length + 1, AllocationType.Commit, MemoryProtection.ExecuteReadWrite);

            if (pathAllocation == IntPtr.Zero)
            {
                Program.getManager().mainLog.Write("Could not allocate memory for path location");
                return false;
            }
#if DEBUG
            Program.getManager().mainLog.Write("Allocated DLL path address is 0x" + pathAllocation.ToString("X8"));
#endif

            byte[] pathBytes = Encoding.ASCII.GetBytes(Path);

            if (!WriteProcessMemory(ProcessHandle, pathAllocation, pathBytes, (uint)pathBytes.Length, out bytesWritten))
            {
                Program.getManager().mainLog.Write("Could not write process memory");
                return false;
            }

            ClientId clientid = new ClientId();
            threadID = new IntPtr();
            RtlCreateUserThread(ProcessHandle, IntPtr.Zero, false, 0, (uint)0, IntPtr.Zero, lpLLAddress, pathAllocation, out threadID, out clientid);

            if (threadID == IntPtr.Zero)
            {
                Program.getManager().mainLog.Write("Could not create remote thread");
                return false;
            }
#if DEBUG
            //Program.getManager().mainLog.Write("Thread Status is " + threadStatus);
            Program.getManager().mainLog.Write("Thread ID is " + threadID);
#endif
            uint responseCode = WaitForSingleObject(threadID, 5000);

            if (responseCode != 0x00000000L)
            {
                Program.getManager().mainLog.Write("Thread did not finish in a timely manner!", Log.Level.Debug);
                Program.getManager().mainLog.Write("Last error is: " + Marshal.GetLastWin32Error(), Log.Level.Debug);
                return false;
            }

            CloseHandle(ProcessHandle);
#if DEBUG
            Program.getManager().mainLog.Write("Initialization finished -- last error : " + Marshal.GetLastWin32Error());
#endif
            return true;
        }

        public static dvar getDvar(int pID, String DVAR, IntPtr lastMemoryLocation)
        {
            dvar requestedDvar = new dvar();
            IntPtr ProcessHandle = OpenProcess(ProcessAccessFlags.All, false, pID);

            if (lastMemoryLocation != IntPtr.Zero)
            {
                if (!VirtualFreeEx(ProcessHandle, lastMemoryLocation, 0, AllocationType.Release))
                    Program.getManager().mainLog.Write("Virtual free failed during cleanup-- Error #" + Marshal.GetLastWin32Error(), Log.Level.Debug);
            }

            IntPtr memoryForDvarName = allocateAndWrite(Encoding.ASCII.GetBytes(DVAR + "\0"), ProcessHandle);

            if (memoryForDvarName == IntPtr.Zero)
            {
                Program.getManager().mainLog.Write("Unable to allocate memory for dvar name", Log.Level.Debug);
                return requestedDvar;
            }

            setDvarCurrentPtr(0x2089E04, memoryForDvarName, ProcessHandle); // sv_allowedclan1
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
                Program.getManager().mainLog.Write("UNABLE TO ALLOCATE MEMORY FOR CODE");
                                                                  
            IntPtr ThreadHandle = CreateRemoteThread(ProcessHandle, IntPtr.Zero, 0, codeAllocation, IntPtr.Zero, 0, out threadID);
            if (ThreadHandle == null || ThreadHandle == IntPtr.Zero)
                return requestedDvar;

            WaitForSingleObject(ThreadHandle, Int32.MaxValue); //  gg if thread doesn't finish

            if (!VirtualFreeEx(ProcessHandle, codeAllocation, 0, AllocationType.Release))
                Program.getManager().mainLog.Write(Marshal.GetLastWin32Error());
            if (!VirtualFreeEx(ProcessHandle, memoryForDvarName, 0, AllocationType.Release))
                Program.getManager().mainLog.Write(Marshal.GetLastWin32Error());*/
#endif
            Thread.Sleep(120);
            int dvarLoc = getIntFromPointer(0x2089E04, (int)ProcessHandle); // this is where the dvar is stored

            if (dvarLoc == 0)
                return requestedDvar;

            dvarLoc = getIntFromPointer(dvarLoc + 0x10, (int)ProcessHandle);

            requestedDvar = getDvar(dvarLoc, ProcessHandle);
            CloseHandle(ProcessHandle);

            return requestedDvar;
        }
    }
}
