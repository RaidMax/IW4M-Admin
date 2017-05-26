using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Net;
using System.Threading;
using SharedLibrary;
using System.IO;
using SharedLibrary.Network;
using System.Threading.Tasks;

namespace IW4MAdmin
{
    class Manager : SharedLibrary.Interfaces.IManager
    {
        static Manager Instance;
        public List<Server> Servers { get; private set; }
        List<Command> Commands;
        Kayak.IScheduler webServiceTask;
        Thread WebThread;
        public bool Running { get; private set; }
#if FTP_LOG
        const double UPDATE_FREQUENCY = 15000;
#else
        const double UPDATE_FREQUENCY = 300;
#endif

        public Log Logger;

        private Manager()
        {
            IFile logFile = new IFile("Logs/IW4MAdminManager.log", true);
            Logger = new Log(logFile, Log.Level.Production, 0);
            Servers = new List<Server>();
            Commands = new List<Command>();
        }

        public List<Server> GetServers()
        {
            return Servers;
        }

        public List<Command> GetCommands()
        {
            return Commands;
        }

        public static Manager GetInstance()
        {
            return Instance == null ? Instance = new Manager() : Instance;
        }

        public void Init()
        {
            var Configs = Directory.EnumerateFiles("config/servers").Where(x => x.Contains(".cfg"));

            if (Configs.Count() == 0)
                Config.Generate();

            PluginImporter.Load();

            foreach (var file in Configs)
            {
                var Conf = Config.Read(file);
                var ServerInstance = new IW4MServer(this, Conf.IP, Conf.Port, Conf.Password);

                Task.Run(async () =>
                {
                    try
                    {
                        await ServerInstance.Initialize();
                        Servers.Add(ServerInstance);
                        Logger.Write($"Now monitoring {ServerInstance.Hostname}", Log.Level.Production);
                    }

                    catch (SharedLibrary.Exceptions.ServerException e)
                    {
                        Logger.Write($"Not monitoring server {Conf.IP}:{Conf.Port} due to uncorrectable errors", Log.Level.Production);
                        if (e.GetType() == typeof(SharedLibrary.Exceptions.DvarException))
                            Logger.Write($"Could not get the dvar value for {(e as SharedLibrary.Exceptions.DvarException).Data["dvar_name"]} (ensure the server has a map loaded)", Log.Level.Production);
                        else if (e.GetType() == typeof(SharedLibrary.Exceptions.NetworkException))
                            Logger.Write("Could not communicate with the server (ensure the configuration is correct)", Log.Level.Production);
                    }
                });

            }

            SharedLibrary.WebService.Init();
            webServiceTask = WebService.getScheduler();

            WebThread = new Thread(webServiceTask.Start);
            WebThread.Name  = "Web Thread";
            WebThread.Start();

            while (Servers.Count < 1)
                Thread.Sleep(500);

            Running = true;
        }
        

        public void Start()
        {
            int Processed;
            DateTime Start;

            while(Running)
            {
                Processed = 0;
                Start = DateTime.Now;
                foreach (Server S in Servers)
                    Processed += S.ProcessUpdatesAsync().Result;

                // ideally we don't want to sleep on the thread, but polling 
                // as much as possible will use unnecessary CPU
                int ElapsedTime = (int)(DateTime.Now - Start).TotalMilliseconds;
                while ((Processed != Servers.Count || ElapsedTime < UPDATE_FREQUENCY) && Running)
                {
                    Thread.Sleep((int)(UPDATE_FREQUENCY - ElapsedTime));
                    ElapsedTime = (int)(DateTime.Now - Start).TotalMilliseconds;
                }
            }
#if !DEBUG
            foreach (var S in Servers)
                S.Broadcast("^1IW4MAdmin going offline!");
#endif
            Servers.Clear();
            WebThread.Abort();
            webServiceTask.Stop();
        }

           
        public void Stop()
        {
            Running = false;
        }
    }
}
