using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SharedLibrary;
using System.IO;
using System.Threading.Tasks;

namespace IW4MAdmin
{
    class Manager : SharedLibrary.Interfaces.IManager
    {
        static Manager Instance;
        public List<Server> Servers { get; private set; }
        List<SharedLibrary.Helpers.AsyncStatus> TaskStatuses;
        Database ClientDatabase;
        Database AliasesDatabase;
        SharedLibrary.Interfaces.IPenaltyList ClientPenalties;
        List<Command> Commands;
        List<SharedLibrary.Helpers.MessageToken> MessageTokens;
        Kayak.IScheduler webServiceTask;
        Thread WebThread;
        public SharedLibrary.Interfaces.ILogger Logger { get; private set; }
        public bool Running { get; private set; }
#if FTP_LOG
        const int UPDATE_FREQUENCY = 15000;
#else
        const int UPDATE_FREQUENCY = 300;
#endif

        private Manager()
        {
            Logger = new Logger("Logs/IW4MAdmin.log");
            Servers = new List<Server>();
            Commands = new List<Command>();
            TaskStatuses = new List<SharedLibrary.Helpers.AsyncStatus>();
            MessageTokens = new List<SharedLibrary.Helpers.MessageToken>();

            ClientDatabase = new ClientsDB("Database/clients.rm");
            AliasesDatabase = new AliasesDB("Database/aliases.rm");
            ClientPenalties = new PenaltyList();
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
            return Instance ?? (Instance = new Manager());
        }

        public void Init()
        {
            SharedLibrary.WebService.Init();
            SharedLibrary.Plugins.PluginImporter.Load(this);

            foreach (var Plugin in SharedLibrary.Plugins.PluginImporter.ActivePlugins)
            {
                try
                {
                    Plugin.OnLoadAsync();
                }

                catch (Exception e)
                {
                    Logger.WriteError($"An error occured loading plugin {Plugin.Name}");
                    Logger.WriteDebug($"Exception: {e.Message}");
                    Logger.WriteDebug($"Stack Trace: {e.StackTrace}");
                }
            }

            var Configs = Directory.EnumerateFiles("config/servers").Where(x => x.Contains(".cfg"));

            if (Configs.Count() == 0)
                ServerConfig.Generate();

            foreach (var file in Configs)
            {
                var Conf = ServerConfig.Read(file);
                var ServerInstance = new IW4MServer(this, Conf.IP, Conf.Port, Conf.Password);

                Task.Run(async () =>
                {
                    try
                    {
                        await ServerInstance.Initialize();
                        Servers.Add(ServerInstance);

                        // this way we can keep track of execution time and see if problems arise.
                        var Status = new SharedLibrary.Helpers.AsyncStatus(ServerInstance, UPDATE_FREQUENCY);
                        TaskStatuses.Add(Status);

                        Logger.WriteVerbose($"Now monitoring {ServerInstance.Hostname}");
                    }

                    catch (SharedLibrary.Exceptions.ServerException e)
                    {
                        Logger.WriteWarning($"Not monitoring server {Conf.IP}:{Conf.Port} due to uncorrectable errors");
                        if (e.GetType() == typeof(SharedLibrary.Exceptions.DvarException))
                            Logger.WriteError($"Could not get the dvar value for {(e as SharedLibrary.Exceptions.DvarException).Data["dvar_name"]} (ensure the server has a map loaded)");
                        else if (e.GetType() == typeof(SharedLibrary.Exceptions.NetworkException))
                            Logger.WriteError("Could not communicate with the server (ensure the configuration is correct)");
                    }
                });

            }

            webServiceTask = WebService.GetScheduler();

            WebThread = new Thread(webServiceTask.Start)
            {
                Name = "Web Thread"
            };

            WebThread.Start();

            Running = true;
        }
        
        public void Start()
        {
            while (Running)
            {
                foreach (var Status in TaskStatuses)
                {
                    if (Status.RequestedTask == null || Status.RequestedTask.IsCompleted)
                    {
                        Status.Update(new Task(() => (Status.Dependant as Server).ProcessUpdatesAsync(Status.GetToken())));
                        if (Status.RunAverage > 500)
                            Logger.WriteWarning($"Update task average execution is longer than desired for {(Status.Dependant as Server).GetIP()}::{(Status.Dependant as Server).GetPort()} [{Status.RunAverage}ms]");
                    }
                }

                Thread.Sleep(UPDATE_FREQUENCY);
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

        public ClientsDB GetClientDatabase()
        {
            return ClientDatabase as ClientsDB;
        }

        public AliasesDB GetAliasesDatabase()
        {
            return AliasesDatabase as AliasesDB;
        }

        public SharedLibrary.Interfaces.IPenaltyList GetClientPenalties()
        {
            return ClientPenalties;
        }

        public SharedLibrary.Interfaces.ILogger GetLogger()
        {
            return Logger;
        }

        public IList<SharedLibrary.Helpers.MessageToken> GetMessageTokens()
        {
            return MessageTokens;
        }

        public IList<Player> GetActiveClients()
        {
            var ActiveClients = new List<Player>();

            foreach (var server in Servers)
                ActiveClients.AddRange(server.Players.Where(p => p != null));

            return ActiveClients;
        }
    }
}
