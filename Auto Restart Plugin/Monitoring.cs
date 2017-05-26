using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using System.Management;
using SharedLibrary;

namespace Auto_Restart_Plugin
{
    static class Monitoring
    {
        [DllImport("kernel32")]
        public static extern bool DeleteFile(string name);

        public static void Restart(Server goodBye)
        {
            _Restart(goodBye);
        }

        private static void _Restart(Server goodBye)
        {
            try
            {
                string cmdLine = GetCommandLine(Process.GetProcessById(goodBye.pID()));
                var info = new ProcessStartInfo();

                // if we don't delete this, we get a prompt..
                DeleteFile(Process.GetProcessById(goodBye.pID()).MainModule.FileName + ":Zone.Identifier");

                //info.WorkingDirectory = goodBye.Basepath;
                info.Arguments = cmdLine;
                info.FileName = Process.GetProcessById(goodBye.pID()).MainModule.FileName;
               // goodBye.executeCommand("killserver");

                Process.GetProcessById(Process.GetProcessById(goodBye.pID()).Parent().Id).Kill();
                Process.GetProcessById(goodBye.pID()).Kill();

                Process.Start(info);
            }

            catch (Exception E)
            {
                goodBye.Log.Write("SOMETHING FUCKED UP BEYOND ALL REPAIR " + E.ToString());
            }
        }

        public static int shouldRestart()
        {
            var curTime = DateTime.Now;
            DateTime restartTime = new DateTime(curTime.Year, curTime.Month, curTime.Day, 4, 0, 0);
            var a =  Math.Floor((restartTime - curTime).TotalMilliseconds / 1000);
            if (a > 0 && a < 2) // just in case of precision
                return 0;
            else
            {
                switch((int)a)
                {
                    case 300:
                        return 300;
                    case 120:
                        return 120;
                    case 60:
                        return 60;
                    case 30:
                        return 30;
                    default:
                        return 1337;
                }
            }
        }

        //http://stackoverflow.com/questions/2633628/can-i-get-command-line-arguments-of-other-processes-from-net-c
        private static string GetCommandLine(this Process process)
        {
            var commandLine = new StringBuilder();
            commandLine.Append(" ");
            using (var searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
            {
                foreach (var @object in searcher.Get())
                {
                    if (@object["CommandLine"].ToString().Contains("iw4m"))
                        commandLine.Append(@object["CommandLine"].ToString().Substring(4));
                    else
                        commandLine.Append(@object["CommandLine"]);
                    commandLine.Append(" ");
                }
            }

            return commandLine.ToString().Trim();
        }
    }


    public static class ProcessExtensions
    {
        private static string FindIndexedProcessName(int pid)
        {
            var processName = Process.GetProcessById(pid).ProcessName;
            var processesByName = Process.GetProcessesByName(processName);
            string processIndexdName = null;

            for (var index = 0; index < processesByName.Length; index++)
            {
                processIndexdName = index == 0 ? processName : processName + "#" + index;
                var processId = new PerformanceCounter("Process", "ID Process", processIndexdName);
                if ((int)processId.NextValue() == pid)
                {
                    return processIndexdName;
                }
            }

            return processIndexdName;
        }

        private static Process FindPidFromIndexedProcessName(string indexedProcessName)
        {
            var parentId = new PerformanceCounter("Process", "Creating Process ID", indexedProcessName);
            return Process.GetProcessById((int)parentId.NextValue());
        }

        public static Process Parent(this Process process)
        {
            return FindPidFromIndexedProcessName(FindIndexedProcessName(process.Id));
        }
    }
}
