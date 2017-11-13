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
        Database ClientDatabase;
        Database AliasesDatabase;
        IPenaltyList ClientPenalties;
        List<Command> Commands;
        List<MessageToken> MessageTokens;
        Kayak.IScheduler webServiceTask;
        Thread WebThread;
        List<Player> PrivilegedClients;
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

            ClientDatabase = new ClientsDB("Database/clients.rm");
            AliasesDatabase = new AliasesDB("Database/aliases.rm");
            ClientPenalties = new PenaltyList();
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
            if ((ClientDatabase as ClientsDB).GetOwner() == null)
                Commands.Add(new COwner("owner", "claim ownership of the server", "o", Player.Permission.User, 0, false));

            Commands.Add(new CQuit("quit", "quit IW4MAdmin", "q", Player.Permission.Owner, 0, false));
            Commands.Add(new CKick("kick", "kick a player by name. syntax: !kick <player> <reason>.", "k", Player.Permission.Trusted, 2, true));
            Commands.Add(new CSay("say", "broadcast message to all players. syntax: !say <message>.", "s", Player.Permission.Moderator, 1, false));
            Commands.Add(new CTempBan("tempban", "temporarily ban a player for for specified time (defaults to 1 hour). syntax: !tempban <player> <time>(m|h|d|w|y) <reason>.", "tb", Player.Permission.Moderator, 2, true));
            Commands.Add(new CBan("ban", "permanently ban a player from the server. syntax: !ban <player> <reason>", "b", Player.Permission.SeniorAdmin, 2, true));
            Commands.Add(new CWhoAmI("whoami", "give information about yourself. syntax: !whoami.", "who", Player.Permission.User, 0, false));
            Commands.Add(new CList("list", "list active clients. syntax: !list.", "l", Player.Permission.Moderator, 0, false));
            Commands.Add(new CHelp("help", "list all available commands. syntax: !help.", "h", Player.Permission.User, 0, false));
            Commands.Add(new CFastRestart("fastrestart", "fast restart current map. syntax: !fastrestart.", "fr", Player.Permission.Moderator, 0, false));
            Commands.Add(new CMapRotate("maprotate", "cycle to the next map in rotation. syntax: !maprotate.", "mr", Player.Permission.Administrator, 0, false));
            Commands.Add(new CSetLevel("setlevel", "set player to specified administration level. syntax: !setlevel <player> <level>.", "sl", Player.Permission.Owner, 2, true));
            Commands.Add(new CUsage("usage", "get current application memory usage. syntax: !usage.", "us", Player.Permission.Moderator, 0, false));
            Commands.Add(new CUptime("uptime", "get current application running time. syntax: !uptime.", "up", Player.Permission.Moderator, 0, false));
            Commands.Add(new CWarn("warn", "warn player for infringing rules. syntax: !warn <player> <reason>.", "w", Player.Permission.Trusted, 2, true));
            Commands.Add(new CWarnClear("warnclear", "remove all warning for a player. syntax: !warnclear <player>.", "wc", Player.Permission.Trusted, 1, true));
            Commands.Add(new CUnban("unban", "unban player by database id. syntax: !unban @<id>.", "ub", Player.Permission.SeniorAdmin, 1, true));
            Commands.Add(new CListAdmins("admins", "list currently connected admins. syntax: !admins.", "a", Player.Permission.User, 0, false));
            Commands.Add(new CLoadMap("map", "change to specified map. syntax: !map", "m", Player.Permission.Administrator, 1, false));
            Commands.Add(new CFindPlayer("find", "find player in database. syntax: !find <player>", "f", Player.Permission.SeniorAdmin, 1, false));
            Commands.Add(new CListRules("rules", "list server rules. syntax: !rules", "r", Player.Permission.User, 0, false));
            Commands.Add(new CPrivateMessage("privatemessage", "send message to other player. syntax: !pm <player> <message>", "pm", Player.Permission.User, 2, true));
            Commands.Add(new CFlag("flag", "flag a suspicious player and announce to admins on join. syntax !flag <player> <reason>:", "fp", Player.Permission.Moderator, 2, true));
            Commands.Add(new CReport("report", "report a player for suspicious behaivor. syntax !report <player> <reason>", "rep", Player.Permission.User, 2, true));
            Commands.Add(new CListReports("reports", "get most recent reports. syntax !reports", "reps", Player.Permission.Moderator, 0, false));
            Commands.Add(new CMask("mask", "hide your online presence from online admin list. syntax: !mask", "hide", Player.Permission.Administrator, 0, false));
            Commands.Add(new CListBanInfo("baninfo", "get information about a ban for a player. syntax: !baninfo <player>", "bi", Player.Permission.Moderator, 1, true));
            Commands.Add(new CListAlias("alias", "get past aliases and ips of a player. syntax: !alias <player>", "known", Player.Permission.Moderator, 1, true));
            Commands.Add(new CExecuteRCON("rcon", "send rcon command to server. syntax: !rcon <command>", "rcon", Player.Permission.Owner, 1, false));
            Commands.Add(new CFindAllPlayers("findall", "find a player by their aliase(s). syntax: !findall <player>", "fa", Player.Permission.Moderator, 1, false));
            Commands.Add(new CPlugins("plugins", "view all loaded plugins. syntax: !plugins", "p", Player.Permission.Administrator, 0, false));
            Commands.Add(new CIP("getexternalip", "view your external IP address. syntax: !ip", "ip", Player.Permission.User, 0, false));

            foreach (Command C in SharedLibrary.Plugins.PluginImporter.ActiveCommands)
                Commands.Add(C);
            #endregion

            #region ADMINS
            PrivilegedClients = GetClientDatabase().GetAdmins();
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

        public ClientsDB GetClientDatabase()
        {
            return ClientDatabase as ClientsDB;
        }

        public AliasesDB GetAliasesDatabase()
        {
            return AliasesDatabase as AliasesDB;
        }

        public IPenaltyList GetClientPenalties()
        {
            return ClientPenalties;
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

        public IList<Player> GetAliasClients(Player Origin)
        {
            List<int> databaseIDs = new List<int>();

            foreach (Aliases A in GetAliases(Origin))
                databaseIDs.Add(A.Number);

            return GetClientDatabase().GetPlayers(databaseIDs);
        }

        public IList<Aliases> GetAliases(Player Origin)
        {
            List<Aliases> allAliases = new List<Aliases>();

            if (Origin == null)
                return allAliases;

            Aliases currentIdentityAliases = GetAliasesDatabase().GetPlayerAliases(Origin.DatabaseID);

            if (currentIdentityAliases == null)
                return allAliases;

            GetAliases(allAliases, currentIdentityAliases);
            if (Origin.Alias != null)
                allAliases.Add(Origin.Alias);
            return allAliases;
        }

        public IList<Player> GetPrivilegedClients()
        {
            return PrivilegedClients;
        }

        private void GetAliases(List<Aliases> returnAliases, Aliases currentAlias)
        {
            foreach (String IP in currentAlias.IPS)
            {
                List<Aliases> Matching = GetAliasesDatabase().GetPlayerAliases(IP);
                foreach (Aliases I in Matching)
                {
                    if (!returnAliases.Contains(I) && returnAliases.Find(x => x.Number == I.Number) == null)
                    {
                        returnAliases.Add(I);
                        GetAliases(returnAliases, I);
                    }
                }
            }
        }
    }
}
