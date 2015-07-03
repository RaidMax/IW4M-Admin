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

        public Manager()
        {
            ThreadList = new SortedDictionary<int, Thread>();
        }

        public void Init()
        {
            activePIDs = getCurrentIW4MProcesses();
            Servers = loadServers();

            foreach (Server S in Servers)
            {
                Server IW4MServer = S;
                Thread IW4MServerThread = new Thread(IW4MServer.Monitor);
                ThreadList.Add(IW4MServer.pID(), IW4MServerThread);
                IW4MServerThread.Start();
            }

            while (true)
            {
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
                            activePIDs.Remove(S.pID());
                        }
                    }
                }
                scanForNewServers();
                Utilities.Wait(5);
            }
        }

        private void scanForNewServers()
        {
            List<int> newProcesses = getCurrentIW4MProcesses();
            foreach (int pID in activePIDs)
            {
                bool newProcess = true;
                foreach (int I in newProcesses)
                {
                    if (I == pID)
                        newProcess = false;
                }

                if (newProcess)
                {
                    Server S = loadIndividualServer(pID);
                    Servers.Add(S);
                    Thread IW4MServerThread = new Thread(S.Monitor);
                    ThreadList.Add(pID, IW4MServerThread);

                    IW4MServerThread.Start();
                }
            }

        }

        private bool isIW4MStillRunning(int pID)
        {
            if (pID > 0)
            {
                Process P = Process.GetProcessById(pID);
                if (P.ProcessName.Length == 0)
                {
                    return false;
                    Console.WriteLine("Server with PID #" + pID + " doesn't seem to be running anymore");
                }
                return true;
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
                    dvar net_ip = Utilities.getDvar(0x64A1DF8, (int)Handle);
                    dvar net_port = Utilities.getDvar(0x64A3004, (int)Handle);
                    // unfortunately this needs to be updated for every iw4m build :/
                    dvar rcon_password = Utilities.getDvar(0x1120CC3C, (int)Handle);

                    return new Server(Dns.GetHostAddresses(net_ip.current)[1].ToString(), Convert.ToInt32(net_port.current), rcon_password.current, (int)Handle, pID);
                }

                return null;
            }
            return null;
        }
    }
}
