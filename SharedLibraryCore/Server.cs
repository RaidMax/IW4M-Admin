using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Database.Models;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Data.Models;
using Microsoft.Extensions.DependencyInjection;

namespace SharedLibraryCore
{
    public abstract class Server : IGameServer
    {
        protected readonly DefaultSettings DefaultSettings;
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
            T7 = 8,
            SHG1 = 9,
            CSGO = 10
        }

        public Server(ILogger<Server> logger, SharedLibraryCore.Interfaces.ILogger deprecatedLogger, 
            ServerConfiguration config, IManager mgr, IRConConnectionFactory rconConnectionFactory, 
            IGameLogReaderFactory gameLogReaderFactory, IServiceProvider serviceProvider)
        {
            Password = config.Password;
            IP = config.IPAddress;
            Port = config.Port;
            Manager = mgr;
            Logger = deprecatedLogger;
            ServerConfig = config;
            EventProcessing = new SemaphoreSlim(1, 1);
            Clients = new List<EFClient>(new EFClient[64]);
            Reports = new List<Report>();
            ClientHistory = new Queue<PlayerHistory>();
            ChatHistory = new List<ChatInfo>();
            NextMessage = 0;
            CustomSayEnabled = Manager.GetApplicationSettings().Configuration().EnableCustomSayName;
            CustomSayName = Manager.GetApplicationSettings().Configuration().CustomSayName;
            this.gameLogReaderFactory = gameLogReaderFactory;
            RConConnectionFactory = rconConnectionFactory;
            ServerLogger = logger;
            DefaultSettings = serviceProvider.GetRequiredService<DefaultSettings>();
            InitializeTokens();
            InitializeAutoMessages();
        }

        public long EndPoint => IPAddress.TryParse(IP, out _) 
            ? Convert.ToInt64($"{IP.Replace(".", "")}{Port}") 
            : $"{IP.Replace(".", "")}{Port}".GetStableHashCode();

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
                return GetClientsAsList().Where(p => p.Name?.StripColors()?.ToLower() == pName.ToLower()).ToList();
            }

            return GetClientsAsList().Where(p => (p.Name?.StripColors()?.ToLower() ?? "").Contains(pName.ToLower())).ToList();
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
            var formattedMessage = string.Format(RconParser.Configuration.CommandPrefixes.Say ?? "",
                $"{(CustomSayEnabled && GameName == Game.IW4 ? $"{CustomSayName}: " : "")}{message.FormatMessageForEngine(RconParser.Configuration.ColorCodeMapping)}");
            ServerLogger.LogDebug("All-> {Message}",
                message.FormatMessageForEngine(RconParser.Configuration.ColorCodeMapping).StripColors());

            var e = new GameEvent
            {
                Type = GameEvent.EventType.Broadcast,
                Data = formattedMessage,
                Owner = this,
                Origin = sender,
            };

            Manager.AddEvent(e);
            return e;
        }
        
        public void Broadcast(IEnumerable<string> messages, EFClient sender = null)
        {
            foreach (var message in messages)
            {
#pragma warning disable 4014
                Broadcast(message, sender).WaitAsync();
#pragma warning restore 4014
            }
        }
        

        /// <summary>
        /// Send a message to a particular players
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="targetClient">EFClient to send message to</param>
        protected async Task Tell(string message, EFClient targetClient)
        {
            var engineMessage = message.FormatMessageForEngine(RconParser.Configuration.ColorCodeMapping);
            
            if (!Utilities.IsDevelopment)
            {
                var temporalClientId = targetClient.GetAdditionalProperty<string>("ConnectionClientId");
                var parsedClientId = string.IsNullOrEmpty(temporalClientId) ? (int?)null : int.Parse(temporalClientId);
                var clientNumber = parsedClientId ?? targetClient.ClientNumber;

                var formattedMessage = string.Format(RconParser.Configuration.CommandPrefixes.Tell,
                    clientNumber,
                    $"{(CustomSayEnabled && GameName == Game.IW4 ? $"{CustomSayName}: " : "")}{engineMessage}");
                if (targetClient.ClientNumber > -1 && message.Length > 0 && targetClient.Level != EFClient.Permission.Console)
                    await this.ExecuteCommandAsync(formattedMessage);
            }
            else
            {
                ServerLogger.LogDebug("Tell[{ClientNumber}]->{Message}", targetClient.ClientNumber,
                    message.FormatMessageForEngine(RconParser.Configuration.ColorCodeMapping).StripColors());
            }

            if (targetClient.Level == EFClient.Permission.Console)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                using (LogContext.PushProperty("Server", ToString()))
                {
                    ServerLogger.LogInformation("Command output received: {Message}",
                        engineMessage.StripColors());
                }
                Console.WriteLine(engineMessage.StripColors());
                Console.ForegroundColor = ConsoleColor.Gray;
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
        /// <param name="reason">Reason for kicking</param>
        /// <param name="Target">EFClient to kick</param>
        public Task Kick(String reason, EFClient Target, EFClient Origin) => Kick(reason, Target, Origin, null);
        public abstract Task Kick(string reason, EFClient target, EFClient origin, EFPenalty originalPenalty);

        /// <summary>
        /// Temporarily ban a player ( default 1 hour ) from the server
        /// </summary>
        /// <param name="reason">Reason for banning the player</param>
        /// <param name="Target">The player to ban</param>
        abstract public Task TempBan(String reason, TimeSpan length, EFClient Target, EFClient Origin);

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
        /// <param name="targetClient">I don't remember what this is for</param>
        /// <returns></returns>
        abstract public Task Unban(string reason, EFClient targetClient, EFClient originClient);

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

        public abstract Task<long> GetIdForServer(Server server = null);

        // Objects
        public IManager Manager { get; protected set; }
        [Obsolete]
        public SharedLibraryCore.Interfaces.ILogger Logger { get; private set; }
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
        public string GametypeName => DefaultSettings.Gametypes.FirstOrDefault(gt => gt.Game == GameName)?.Gametypes
            ?.FirstOrDefault(gt => gt.Name == Gametype)?.Alias ?? Gametype;
        public string GamePassword { get; protected set; }
        public Map CurrentMap { get; set; }
        public int ClientNum
        {
            get
            {
                return Clients.ToArray().Count(p => p != null && !p.IsBot);
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
        /// <summary>
        /// this is actually the hostname now
        /// </summary>
        public string IP { get; protected set; }
        public IPEndPoint ResolvedIpEndPoint { get; protected set; }
        public string Version { get; protected set; }
        public bool IsInitialized { get; set; }
        protected readonly ILogger ServerLogger;

        public int Port { get; private set; }
        protected string FSGame;
        protected int NextMessage;
        protected int ConnectionErrors;
        protected List<string> BroadcastMessages;
        protected TimeSpan LastMessage;
        protected DateTime LastPoll;
        protected ManualResetEventSlim OnRemoteCommandResponse;
        protected IGameLogReaderFactory gameLogReaderFactory;
        protected IRConConnectionFactory RConConnectionFactory;

        // only here for performance
        private readonly bool CustomSayEnabled;
        protected readonly string CustomSayName;
    }
}
