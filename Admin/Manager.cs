using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net;
using System.Threading;

namespace IW4MAdmin
{
    class Manager
    {
        private List<Server> Servers;
        private SortedDictionary<int, Thread> ThreadList;
        private List<int> activePIDs;
        private Log mainLog;
        private bool initialized = false;

        public Manager()
        {
            ThreadList = new SortedDictionary<int, Thread>();
            file logFile = new file("IW4MAdmin_ApplicationLog.log");
            mainLog = new Log(logFile, Log.Level.All, 0);
        }

        public void Init()
        {
            activePIDs = getCurrentIW4MProcesses();

            if (activePIDs.Count == 0)
            {
                mainLog.Write("No viable IW4M instances detected.", Log.Level.All);
                mainLog.Write("Shutting Down...", Log.Level.All);
                Utilities.Wait(5);
                return;
            }

            Servers = loadServers();

            foreach (Server S in Servers)
            {
                Server IW4MServer = S;
                Thread IW4MServerThread = new Thread(IW4MServer.Monitor);
                ThreadList.Add(IW4MServer.pID(), IW4MServerThread);
                IW4MServerThread.Start();

                //mainLog.Write("Now monitoring the server running on port " + IW4MServer.getPort(), Log.Level.All);
            }

            initialized = true;

            while (true)
            {
                List<Server> defunctServers = new List<Server>();
                lock (Servers)
                {
             
                    foreach (Server S in Servers)
                    {
                        if (S == null)
                            continue;

                        if (!isIW4MStillRunning(S.pID()))
                        {
                            Thread Defunct = ThreadList[S.pID()];
                            if (Defunct != null)
                            {
                                Defunct.Abort();
                                ThreadList[S.pID()] = null;
                            }
                            mainLog.Write("Server with PID #" + S.pID() + " no longer appears to be running.", Log.Level.All);
                            activePIDs.Remove(S.pID());
                            defunctServers.Add(S);
                        }
                    }
                }

                foreach (Server S in defunctServers)
                    Servers.Remove(S);
                defunctServers = null;

                scanForNewServers();
                Utilities.Wait(5);
            }
        }

        public List<Server> getServers()
        {
            return Servers;
        }

        private void scanForNewServers()
        {
            List<int> newProcesses = getCurrentIW4MProcesses();
            foreach (int pID in newProcesses)
            {
                bool newProcess = true;
                foreach (int I in activePIDs)
                {
                    if (I == pID)
                        newProcess = false;
                }

                if (newProcess)
                {

                    if (!ThreadList.ContainsKey(pID))
                    {
                        Server S = loadIndividualServer(pID);
                        Servers.Add(S);
                        Thread IW4MServerThread = new Thread(S.Monitor);
                        ThreadList.Add(pID, IW4MServerThread);
                        mainLog.Write("New server dectected on port " + S.getPort(), Log.Level.All);
                        IW4MServerThread.Start();
                    }
                }
            }

        }

        private bool isIW4MStillRunning(int pID)
        {
            if (pID > 0)
            {
                try
                {
                    Process P = Process.GetProcessById(pID);
                    return true;
                }

                catch (System.ArgumentException)
                {
                    return false;
                }

            }

            return false;
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        public static extern bool ReadProcessMemory(int hProcess, int lpBaseAddress, byte[] lpBuffer, int dwSize, ref int lpNumberOfBytesRead);

        private List<int> getCurrentIW4MProcesses()
        {
            List<int> PIDs = new List<int>();
            foreach (Process P in Process.GetProcessesByName("iw4m"))
            {
                IntPtr Handle = OpenProcess(0x10, false, P.Id);
                Byte[] isClient = new Byte[1];
                int numberRead = 0;
                ReadProcessMemory((int)Handle, 0x5DEC04, isClient, 1, ref numberRead);

                if (isClient[0] == 0)
                    PIDs.Add(P.Id);
            }

            return PIDs;
        }

        private List<Server> loadServers()
        {
            List<Server> activeServers = new List<Server>();
            foreach (int pID in activePIDs)
            {
                Server S = loadIndividualServer(pID);
                if (S != null)
                    activeServers.Add(S);
            }
            return activeServers;
        }

        private Server loadIndividualServer(int pID)
        {
            if (pID > 0)
            {
                IntPtr Handle = OpenProcess(0x10, false, pID);
                if (Handle != null)
                {
                    int timeWaiting = 0;
     
                    bool sv_running = false;
                    

                    while(!sv_running) // server is still booting up
                    {
                        int sv_runningPtr = Utilities.getIntFromPointer(0x1AD7934, (int)Handle) + 0x10; // where the dvar_t struct is stored + the offset for current value
                        sv_running = Utilities.getBoolFromPointer(sv_runningPtr, (int)Handle);
                        Utilities.Wait(1);
                        timeWaiting++;

                        if (timeWaiting > 30) // don't want to get stuck waiting forever if the server is frozen
                            return null;
                    }

                    Utilities.Wait(5);

                    dvar net_ip = Utilities.getDvar(0x64A1DF8, (int)Handle);
                    dvar net_port = Utilities.getDvar(0x64A3004, (int)Handle);

                    return new Server(net_ip.current, Convert.ToInt32(net_port.current), "", (int)Handle, pID);
                }

                return null;
            }
            return null;
        }

        public bool isReady()
        {
            return initialized;
        }
    }
}
