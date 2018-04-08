using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.IO;
using System.Threading.Tasks;

using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Objects;
using SharedLibraryCore.Services;
using IW4MAdmin.Application.API;
using Microsoft.Extensions.Configuration;
using WebfrontCore;
using SharedLibraryCore.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IW4MAdmin.Application
{
    public class ApplicationManager : IManager
    {
        private List<Server> _servers;
        public List<Server> Servers => _servers.OrderByDescending(s => s.ClientNum).ToList();
        public Dictionary<int, Player> PrivilegedClients { get; set; }
        public ILogger Logger { get; private set; }
        public bool Running { get; private set; }
        public EventHandler<Event> ServerEventOccurred { get; private set; }

        static ApplicationManager Instance;
        List<AsyncStatus> TaskStatuses;
        List<Command> Commands;
        List<MessageToken> MessageTokens;
        ClientService ClientSvc;
        AliasService AliasSvc;
        PenaltyService PenaltySvc;
        BaseConfigurationHandler<ApplicationConfiguration> ConfigHandler;
        EventApi Api;
#if FTP_LOG
        const int UPDATE_FREQUENCY = 700;
#else
        const int UPDATE_FREQUENCY = 450;
#endif

        private ApplicationManager()
        {
            Logger = new Logger($@"{Utilities.OperatingDirectory}IW4MAdmin.log");
            _servers = new List<Server>();
            Commands = new List<Command>();
            TaskStatuses = new List<AsyncStatus>();
            MessageTokens = new List<MessageToken>();
            ClientSvc = new ClientService();
            AliasSvc = new AliasService();
            PenaltySvc = new PenaltyService();
            PrivilegedClients = new Dictionary<int, Player>();
            Api = new EventApi();
            ServerEventOccurred += Api.OnServerEvent;
            ConfigHandler = new BaseConfigurationHandler<ApplicationConfiguration>("IW4MAdminSettings");
            Console.CancelKeyPress += new ConsoleCancelEventHandler(OnCancelKey);
        }

        private void OnCancelKey(object sender, ConsoleCancelEventArgs args)
        {
            Stop();
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

        public async Task Init()
        {
            #region DATABASE
            var ipList = (await ClientSvc.Find(c => c.Level > Player.Permission.Trusted))
                .Select(c => new
                {
                    c.Password,
                    c.PasswordSalt,
                    c.ClientId,
                    c.Level,
                    c.Name
                });

            foreach (var a in ipList)
            {
                try
                {
                    PrivilegedClients.Add(a.ClientId, new Player()
                    {
                        Name = a.Name,
                        ClientId = a.ClientId,
                        Level = a.Level,
                        PasswordSalt = a.PasswordSalt,
                        Password = a.Password
                    });
                }

                catch (ArgumentException)
                {
                    continue;
                }
            }
            #endregion

            #region CONFIG
            var config = ConfigHandler.Configuration();
            if (config?.Servers == null)
            {
                var newConfig = (ApplicationConfiguration)ConfigHandler.Configuration().Generate();
                ConfigHandler.Set(newConfig);

                newConfig.AutoMessagePeriod = config.AutoMessagePeriod;
                newConfig.AutoMessages = config.AutoMessages;
                newConfig.GlobalRules = config.GlobalRules;
                newConfig.Maps = config.Maps;
                newConfig.Servers = ConfigurationGenerator.GenerateServerConfig(new List<ServerConfiguration>());
                config = newConfig;
                await ConfigHandler.Save();
            }

            else if (config.Servers.Count == 0)
                throw new ServerException("A server configuration in IW4MAdminSettings.json is invalid");


            #region PLUGINS
            SharedLibraryCore.Plugins.PluginImporter.Load(this);

            foreach (var Plugin in SharedLibraryCore.Plugins.PluginImporter.ActivePlugins)
            {
                try
                {
                    await Plugin.OnLoadAsync(this);
                }

                catch (Exception e)
                {
                    Logger.WriteError($"An error occured loading plugin {Plugin.Name}");
                    Logger.WriteDebug($"Exception: {e.Message}");
                    Logger.WriteDebug($"Stack Trace: {e.StackTrace}");
                }
            }
            #endregion

            foreach (var Conf in config.Servers)
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
                    Logger.WriteError($"Not monitoring server {Conf.IPAddress}:{Conf.Port} due to uncorrectable errors");
                    if (e.GetType() == typeof(DvarException))
                        Logger.WriteDebug($"Could not get the dvar value for {(e as DvarException).Data["dvar_name"]} (ensure the server has a map loaded)");
                    else if (e.GetType() == typeof(NetworkException))
                    {
                        Logger.WriteDebug(e.Message);
                        //Logger.WriteDebug($"Internal Exception: {e.Data["internal_exception"]}");
                    }

                    // throw the exception to the main method to stop before instantly exiting
                    throw e;
                }

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
            Commands.Add(new CFlag());
            Commands.Add(new CReport());
            Commands.Add(new CListReports());
            Commands.Add(new CListBanInfo());
            Commands.Add(new CListAlias());
            Commands.Add(new CExecuteRCON());
            Commands.Add(new CPlugins());
            Commands.Add(new CIP());
            Commands.Add(new CMask());
            Commands.Add(new CPruneAdmins());
            Commands.Add(new CKillServer());
            Commands.Add(new CSetPassword());

            foreach (Command C in SharedLibraryCore.Plugins.PluginImporter.ActiveCommands)
                Commands.Add(C);
            #endregion

            Running = true;
        }

        public void Start()
        {
            while (Running || TaskStatuses.Count > 0)
            {
                for (int i = 0; i < TaskStatuses.Count; i++)
                {
                    var Status = TaskStatuses[i];

                    // task is read to be rerun
                    if (Status.RequestedTask == null || Status.RequestedTask.Status == TaskStatus.RanToCompletion)
                    {
                        // remove the task when we want to quit and last run has finished
                        if (!Running)
                        {
                            TaskStatuses.RemoveAt(i);
                            continue;
                        }
                        // normal operation
                        else
                        {
                            Status.Update(new Task<bool>(() => { return (Status.Dependant as Server).ProcessUpdatesAsync(Status.GetToken()).Result; }));
                            if (Status.RunAverage > 1000 + UPDATE_FREQUENCY && !(Status.Dependant as Server).Throttled)
                                Logger.WriteWarning($"Update task average execution is longer than desired for {(Status.Dependant as Server)} [{Status.RunAverage}ms]");
                        }
                    }

                    if (Status.RequestedTask.Status == TaskStatus.Faulted)
                    {
                        Logger.WriteWarning($"Update task for  {(Status.Dependant as Server)} faulted, restarting");
                        Status.Abort();
                    }
                }

                Thread.Sleep(UPDATE_FREQUENCY);
            }
#if !DEBUG
            foreach (var S in Servers)
                S.Broadcast("^1IW4MAdmin going offline!").Wait();
#endif
            _servers.Clear();
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

        public IConfigurationHandler<ApplicationConfiguration> GetApplicationSettings() => ConfigHandler;

        public IDictionary<int, Player> GetPrivilegedClients() => PrivilegedClients;

        public IEventApi GetEventApi() => Api;

        public bool ShutdownRequested() => !Running;
    }
}
