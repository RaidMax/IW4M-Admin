using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

using SharedLibrary;
using SharedLibrary.Interfaces;
using SharedLibrary.Commands;
using SharedLibrary.Helpers;
using SharedLibrary.Exceptions;
using SharedLibrary.Objects;
using SharedLibrary.Database;
using SharedLibrary.Database.Models;
using SharedLibrary.Services;

namespace IW4MAdmin
{
    class ApplicationManager : IManager
    {
        private List<Server> _servers;
        public List<Server> Servers => _servers.OrderByDescending(s => s.ClientNum).ToList();
        public ILogger Logger { get; private set; }
        public bool Running { get; private set; }

        static ApplicationManager Instance;
        List<AsyncStatus> TaskStatuses;
        List<Command> Commands;
        List<MessageToken> MessageTokens;
        Kayak.IScheduler webServiceTask;
        Thread WebThread;
        ClientService ClientSvc;
        AliasService AliasSvc;
        PenaltyService PenaltySvc;
#if FTP_LOG
        const int UPDATE_FREQUENCY = 15000;
#else
        const int UPDATE_FREQUENCY = 300;
#endif

        private ApplicationManager()
        {
            Logger = new Logger("Logs/IW4MAdmin.log");
            _servers = new List<Server>();
            Commands = new List<Command>();
            TaskStatuses = new List<AsyncStatus>();
            MessageTokens = new List<MessageToken>();
            ClientSvc = new ClientService();
            AliasSvc = new AliasService();
            PenaltySvc = new PenaltyService();
        }

        public IList<Server> GetServers()
        {
            return Servers;
        }

        public IList<Command> GetCommands()
        {
            return Commands;
        }

        public static ApplicationManager GetInstance()
        {
            return Instance ?? (Instance = new ApplicationManager());
        }

        public void Init()
        {
            #region WEBSERVICE
            SharedLibrary.WebService.Init();
            webServiceTask = WebService.GetScheduler();

            WebThread = new Thread(webServiceTask.Start)
            {
                Name = "Web Thread"
            };

            WebThread.Start();
            #endregion

            #region PLUGINS
            SharedLibrary.Plugins.PluginImporter.Load(this);

            foreach (var Plugin in SharedLibrary.Plugins.PluginImporter.ActivePlugins)
            {
                try
                {
                    Plugin.OnLoadAsync(this);
                }

                catch (Exception e)
                {
                    Logger.WriteError($"An error occured loading plugin {Plugin.Name}");
                    Logger.WriteDebug($"Exception: {e.Message}");
                    Logger.WriteDebug($"Stack Trace: {e.StackTrace}");
                }
            }
            #endregion

            #region CONFIG
            var Configs = Directory.EnumerateFiles("config/servers").Where(x => x.Contains(".cfg"));

            if (Configs.Count() == 0)
                ServerConfigurationGenerator.Generate();

            foreach (var file in Configs)
            {
                var Conf = ServerConfiguration.Read(file);

                Task.Run(async () =>
                {
                    try
                    {
                        var ServerInstance = new IW4MServer(this, Conf);
                        await ServerInstance.Initialize();

                        lock (_servers)
                        {
                            _servers.Add(ServerInstance);
                        }

                        Logger.WriteVerbose($"Now monitoring {ServerInstance.Hostname}");

                        // this way we can keep track of execution time and see if problems arise.
                        var Status = new AsyncStatus(ServerInstance, UPDATE_FREQUENCY);
                        lock (TaskStatuses)
                        {
                            TaskStatuses.Add(Status);
                        }
                    }

                    catch (ServerException e)
                    {
                        Logger.WriteError($"Not monitoring server {Conf.IP}:{Conf.Port} due to uncorrectable errors");
                        if (e.GetType() == typeof(DvarException))
                            Logger.WriteDebug($"Could not get the dvar value for {(e as DvarException).Data["dvar_name"]} (ensure the server has a map loaded)");
                        else if (e.GetType() == typeof(NetworkException))
                        {
                            Logger.WriteDebug(e.Message);
                            Logger.WriteDebug($"Internal Exception: {e.Data["internal_exception"]}");
                        }
                    }
                });
            }
            #endregion

            #region COMMANDS
            if (ClientSvc.GetOwners().Result.Count == 0)
                Commands.Add(new COwner());

            Commands.Add(new CQuit());
            Commands.Add(new CKick());
            Commands.Add(new CSay());
            Commands.Add(new CTempBan());
            Commands.Add(new CBan());
            Commands.Add(new CWhoAmI());
            Commands.Add(new CList());
            Commands.Add(new CHelp());
            Commands.Add(new CFastRestart());
            Commands.Add(new CMapRotate());
            Commands.Add(new CSetLevel());
            Commands.Add(new CUsage());
            Commands.Add(new CUptime());
            Commands.Add(new CWarn());
            Commands.Add(new CWarnClear());
            Commands.Add(new CUnban());
            Commands.Add(new CListAdmins());
            Commands.Add(new CLoadMap());
            Commands.Add(new CFindPlayer());
            Commands.Add(new CListRules());
            Commands.Add(new CPrivateMessage());
            Commands.Add(new CReload());
            Commands.Add(new CFlag());
            Commands.Add(new CReport());
            Commands.Add(new CListReports());
            Commands.Add(new CListBanInfo());
            Commands.Add(new CListAlias());
            Commands.Add(new CExecuteRCON());
            Commands.Add(new CFindAllPlayers());
            Commands.Add(new CPlugins());
            Commands.Add(new CIP());
            Commands.Add(new CMask());

            foreach (Command C in SharedLibrary.Plugins.PluginImporter.ActiveCommands)
                Commands.Add(C);
            #endregion

            Running = true;
        }

        public void Start()
        {
            while (Running)
            {
                for (int i = 0; i < TaskStatuses.Count; i++)
                {
                    var Status = TaskStatuses[i];
                    if (Status.RequestedTask == null || Status.RequestedTask.IsCompleted)
                    {
                        Status.Update(new Task(() => (Status.Dependant as Server).ProcessUpdatesAsync(Status.GetToken())));
                        if (Status.RunAverage > 1000 + UPDATE_FREQUENCY)
                            Logger.WriteWarning($"Update task average execution is longer than desired for {(Status.Dependant as Server).GetIP()}::{(Status.Dependant as Server).GetPort()} [{Status.RunAverage}ms]");
                    }
                }

                Thread.Sleep(UPDATE_FREQUENCY);
            }
#if !DEBUG
            foreach (var S in Servers)
                S.Broadcast("^1IW4MAdmin going offline!");
#endif
            _servers.Clear();
            WebThread.Abort();
            webServiceTask.Stop();
        }


        public void Stop()
        {
            Running = false;
        }

        public ILogger GetLogger()
        {
            return Logger;
        }

        public IList<MessageToken> GetMessageTokens()
        {
            return MessageTokens;
        }

        public IList<Player> GetActiveClients()
        {
            var ActiveClients = new List<Player>();

            foreach (var server in _servers)
                ActiveClients.AddRange(server.Players.Where(p => p != null));

            return ActiveClients;
        }

        public ClientService GetClientService() => ClientSvc;
        public AliasService GetAliasService() => AliasSvc;
        public PenaltyService GetPenaltyService() => PenaltySvc;
    }
}
