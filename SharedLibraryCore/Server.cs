using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using SharedLibraryCore.Helpers;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore
{
    public abstract class Server
    {
        public enum Game
        {
            COD = -1,
            UKN = 0,
            IW3 = 1,
            IW4 = 2,
            IW5 = 3,
            IW6 = 4,
            T4 = 5,
            T5 = 6,
            T6 = 7,
            T7 = 8
        }

        public Server(ServerConfiguration config, IManager mgr, IRConConnectionFactory rconConnectionFactory, IGameLogReaderFactory gameLogReaderFactory)
        {
            Password = config.Password;
            IP = config.IPAddress;
            Port = config.Port;
            Manager = mgr;
            Logger = Manager.GetLogger(this.EndPoint);
            Logger.WriteInfo(this.ToString());
            ServerConfig = config;
            RemoteConnection = rconConnectionFactory.CreateConnection(IP, Port, Password);
            EventProcessing = new SemaphoreSlim(1, 1);
            Clients = new List<EFClient>(new EFClient[18]);
            Reports = new List<Report>();
            ClientHistory = new Queue<PlayerHistory>();
            ChatHistory = new List<ChatInfo>();
            NextMessage = 0;
            CustomSayEnabled = Manager.GetApplicationSettings().Configuration().EnableCustomSayName;
            CustomSayName = Manager.GetApplicationSettings().Configuration().CustomSayName;
            this.gameLogReaderFactory = gameLogReaderFactory;
            InitializeTokens();
            InitializeAutoMessages();
        }

        public long EndPoint => Convert.ToInt64($"{IP.Replace(".", "")}{Port}");

        /// <summary>
        /// Returns list of all current players
        /// </summary>
        /// <returns></returns>
        public List<EFClient> GetClientsAsList()
        {
            return Clients.FindAll(x => x != null && x.NetworkId != 0);
        }

        /// <summary>
        /// Add a player to the server's player list
        /// </summary>
        /// <param name="P">EFClient pulled from memory reading</param>
        /// <returns>True if player added sucessfully, false otherwise</returns>
        public abstract Task<EFClient> OnClientConnected(EFClient P);

        /// <summary>
        /// Remove player by client number
        /// </summary>
        /// <param name="cNum">Client ID of player to be removed</param>
        /// <returns>true if removal succeded, false otherwise</returns>
        public abstract Task OnClientDisconnected(EFClient client);

        /// <summary>
        /// Get a player by name
        /// todo: make this an extension
        /// </summary>
        /// <param name="pName">EFClient name to search for</param>
        /// <returns>Matching player if found</returns>
        public List<EFClient> GetClientByName(String pName)
        {
            if (string.IsNullOrEmpty(pName))
            {
                return new List<EFClient>();
            }

            pName = pName.Trim().StripColors();

            string[] QuoteSplit = pName.Split('"');
            bool literal = false;
            if (QuoteSplit.Length > 1)
            {
                pName = QuoteSplit[1];
                literal = true;
            }
            if (literal)
            {
                return GetClientsAsList().Where(p => p.Name?.ToLower() == pName.ToLower()).ToList();
            }

            return GetClientsAsList().Where(p => (p.Name?.ToLower() ?? "").Contains(pName.ToLower())).ToList();
        }

        virtual public Task<bool> ProcessUpdatesAsync(CancellationToken cts) => (Task<bool>)Task.CompletedTask;

        /// <summary>
        /// Process any server event
        /// </summary>
        /// <param name="E">Event</param>
        /// <returns>True on sucess</returns>
        protected abstract Task<bool> ProcessEvent(GameEvent E);
        public abstract Task ExecuteEvent(GameEvent E);

        /// <summary>
        /// Send a message to all players
        /// </summary>
        /// <param name="message">Message to be sent to all players</param>
        public GameEvent Broadcast(string message, EFClient sender = null)
        {
            string formattedMessage = string.Format(RconParser.Configuration.CommandPrefixes.Say ?? "", $"{(CustomSayEnabled && GameName == Game.IW4 ? $"{CustomSayName}: " : "")}{message.FixIW4ForwardSlash()}");
#if DEBUG == true
            Logger.WriteVerbose(message.StripColors());
#endif

            var e = new GameEvent()
            {
                Type = GameEvent.EventType.Broadcast,
                Data = formattedMessage,
                Owner = this,
                Origin = sender,
            };

            Manager.AddEvent(e);
            return e;
        }

        /// <summary>
        /// Send a message to a particular players
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="target">EFClient to send message to</param>
        protected async Task Tell(string message, EFClient target)
        {
#if !DEBUG
            string formattedMessage = string.Format(RconParser.Configuration.CommandPrefixes.Tell, target.ClientNumber, $"{(CustomSayEnabled && GameName == Game.IW4 ? $"{CustomSayName}: " : "")}{message.FixIW4ForwardSlash()}");
            if (target.ClientNumber > -1 && message.Length > 0 && target.Level != EFClient.Permission.Console)
                await this.ExecuteCommandAsync(formattedMessage);
#else
            Logger.WriteVerbose($"{target.ClientNumber}->{message.StripColors()}");
            await Task.CompletedTask;
#endif

            if (target.Level == EFClient.Permission.Console)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(message.StripColors());
                Console.ForegroundColor = ConsoleColor.Gray;
            }

            // prevent this from queueing up too many command responses
            if (CommandResult.Count > 15)
            {
                CommandResult.RemoveAt(0);
            }

            // it was a remote command so we need to add it to the command result queue
            if (target.ClientNumber < 0)
            {
                CommandResult.Add(new CommandResponseInfo()
                {
                    Response = message.StripColors(),
                    ClientId = target.ClientId
                });
            }
        }

        /// <summary>
        /// Send a message to all admins on the server
        /// </summary>
        /// <param name="message">Message to send out</param>
        public void ToAdmins(String message)
        {
            foreach (var client in GetClientsAsList().Where(c => c.Level > EFClient.Permission.Flagged))
            {
                client.Tell(message);
            }
        }

        /// <summary>
        /// Kick a player from the server
        /// </summary>
        /// <param name="Reason">Reason for kicking</param>
        /// <param name="Target">EFClient to kick</param>
        abstract public Task Kick(String Reason, EFClient Target, EFClient Origin);

        /// <summary>
        /// Temporarily ban a player ( default 1 hour ) from the server
        /// </summary>
        /// <param name="Reason">Reason for banning the player</param>
        /// <param name="Target">The player to ban</param>
        abstract public Task TempBan(String Reason, TimeSpan length, EFClient Target, EFClient Origin);

        /// <summary>
        /// Perm ban a player from the server
        /// </summary>
        /// <param name="Reason">The reason for the ban</param>
        /// <param name="Target">The person to ban</param>
        /// <param name="Origin">The person who banned the target</param>
        abstract public Task Ban(String Reason, EFClient Target, EFClient Origin, bool isEvade = false);

        abstract public Task Warn(String Reason, EFClient Target, EFClient Origin);

        /// <summary>
        /// Unban a player by npID / GUID
        /// </summary>
        /// <param name="npID">npID of the player</param>
        /// <param name="Target">I don't remember what this is for</param>
        /// <returns></returns>
        abstract public Task Unban(string reason, EFClient Target, EFClient Origin);

        /// <summary>
        /// Change the current searver map
        /// </summary>
        /// <param name="mapName">Non-localized map name</param>
        public async Task LoadMap(string mapName)
        {
            await this.ExecuteCommandAsync($"map {mapName}");
        }

        /// <summary>
        /// Initalize the macro variables
        /// </summary>
        abstract public void InitializeTokens();

        /// <summary>
        /// Read the map configuration
        /// </summary>
        protected void InitializeMaps()
        {
            Maps = new List<Map>();
            var gameMaps = Manager.GetApplicationSettings().Configuration().Maps.FirstOrDefault(m => m.Game == GameName);
            if (gameMaps != null)
                Maps.AddRange(gameMaps.Maps);
        }

        /// <summary>
        /// Initialize the messages to be broadcasted
        /// </summary>
        protected void InitializeAutoMessages()
        {
            BroadcastMessages = new List<String>();

            if (ServerConfig.AutoMessages != null)
                BroadcastMessages.AddRange(ServerConfig.AutoMessages);
            BroadcastMessages.AddRange(Manager.GetApplicationSettings().Configuration().AutoMessages);
        }

        public override string ToString()
        {
            return $"{IP}:{Port}";
        }

        protected async Task<bool> ScriptLoaded()
        {
            try
            {
                return (await this.GetDvarAsync("sv_customcallbacks", "0")).Value == "1";
            }

            catch (Exceptions.DvarException)
            {
                return false;
            }
        }

        // Objects
        public IManager Manager { get; protected set; }
        public ILogger Logger { get; private set; }
        public ServerConfiguration ServerConfig { get; private set; }
        public List<Map> Maps { get; protected set; } = new List<Map>();
        public List<Report> Reports { get; set; }
        public List<ChatInfo> ChatHistory { get; protected set; }
        public Queue<PlayerHistory> ClientHistory { get; private set; }
        public Game GameName { get; set; }

        // Info
        private string hostname;
        public string Hostname { get => ServerConfig.CustomHostname ?? hostname; protected set => hostname = value; }
        public string Website { get; protected set; }
        public string Gametype { get; set; }
        public string GamePassword { get; protected set; }
        public Map CurrentMap { get; set; }
        public int ClientNum
        {
            get
            {
                return Clients.Where(p => p != null/* && !p.IsBot*/).Count();
            }
        }
        public int MaxClients { get; protected set; }
        public List<EFClient> Clients { get; protected set; }
        public string Password { get; private set; }
        public bool Throttled { get; protected set; }
        public bool CustomCallback { get; protected set; }
        public string WorkingDirectory { get; protected set; }
        public IRConConnection RemoteConnection { get; protected set; }
        public IRConParser RconParser { get; set; }
        public IEventParser EventParser { get; set; }
        public string LogPath { get; protected set; }
        public bool RestartRequested { get; set; }
        public SemaphoreSlim EventProcessing { get; private set; }

        // Internal
        public string IP { get; protected set; }
        public string Version { get; protected set; }
        public bool IsInitialized { get; set; }

        public int Port { get; private set; }
        protected string FSGame;
        protected int NextMessage;
        protected int ConnectionErrors;
        protected List<string> BroadcastMessages;
        protected TimeSpan LastMessage;
        protected DateTime LastPoll;
        protected ManualResetEventSlim OnRemoteCommandResponse;
        protected IGameLogReaderFactory gameLogReaderFactory;

        // only here for performance
        private readonly bool CustomSayEnabled;
        private readonly string CustomSayName;

        //Remote
        public IList<CommandResponseInfo> CommandResult = new List<CommandResponseInfo>();
    }
}
