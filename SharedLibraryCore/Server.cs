using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Data.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Exceptions;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SharedLibraryCore
{
    public abstract class Server : IGameServer
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
            T7 = 8,
            SHG1 = 9,
            CSGO = 10,
            H1 = 11
        }

        // only here for performance
        private readonly bool CustomSayEnabled;
        protected readonly string CustomSayName;
        protected readonly DefaultSettings DefaultSettings;
        protected readonly ILogger ServerLogger;
        protected List<string> BroadcastMessages;
        protected int ConnectionErrors;
        protected string FSGame;
        protected IGameLogReaderFactory gameLogReaderFactory;

        // Info
        private string hostname;
        protected TimeSpan LastMessage;
        protected DateTime LastPoll;
        protected int NextMessage;
        protected ManualResetEventSlim OnRemoteCommandResponse;
        protected IRConConnectionFactory RConConnectionFactory;

#pragma warning disable CS0612
        public Server(ILogger<Server> logger, Interfaces.ILogger deprecatedLogger,
#pragma warning restore CS0612
            ServerConfiguration config, IManager mgr, IRConConnectionFactory rconConnectionFactory,
            IGameLogReaderFactory gameLogReaderFactory, IServiceProvider serviceProvider)
        {
            Password = config.Password;
            IP = config.IPAddress;
            Port = config.Port;
            Manager = mgr;
#pragma warning disable CS0612
            Logger = deprecatedLogger ?? throw new ArgumentNullException(nameof(deprecatedLogger));
#pragma warning restore CS0612
            ServerConfig = config;
            EventProcessing = new SemaphoreSlim(1, 1);
            Clients = new List<EFClient>(new EFClient[64]);
            Reports = new List<Report>();
            ClientHistory = new ClientHistoryInfo();
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

        // Objects
        public IManager Manager { get; protected set; }

        [Obsolete] public Interfaces.ILogger Logger { get; }

        public ServerConfiguration ServerConfig { get; }
        public List<Map> Maps { get; protected set; } = new List<Map>();
        public List<Report> Reports { get; set; }
        public List<ChatInfo> ChatHistory { get; protected set; }
        public ClientHistoryInfo ClientHistory { get; }
        public Game GameName { get; set; }
        public DateTime? MatchEndTime { get; protected set; }
        public DateTime? MatchStartTime { get; protected set; }

        public string Hostname
        {
            get => ServerConfig.CustomHostname ?? hostname;
            protected set => hostname = value;
        }

        public string Website { get; protected set; }
        public string Gametype { get; set; }

        public string GametypeName => DefaultSettings.Gametypes.FirstOrDefault(gt => gt.Game == GameName)?.Gametypes
            ?.FirstOrDefault(gt => gt.Name == Gametype)?.Alias ?? Gametype;

        public string GamePassword { get; protected set; }
        public Map CurrentMap { get; set; }

        public int ClientNum
        {
            get { return Clients.ToArray().Count(p => p != null && Utilities.IsDevelopment || (!p?.IsBot ?? false)); }
        }

        public int MaxClients { get; protected set; }
        public List<EFClient> Clients { get; protected set; }
        public string Password { get; }
        public bool Throttled { get; protected set; }
        public bool CustomCallback { get; protected set; }
        public string WorkingDirectory { get; protected set; }
        public IRConConnection RemoteConnection { get; protected set; }
        public IRConParser RconParser { get; set; }
        public IEventParser EventParser { get; set; }
        public string LogPath { get; protected set; }
        public bool RestartRequested { get; set; }
        public SemaphoreSlim EventProcessing { get; }

        // Internal
        /// <summary>
        ///     this is actually the hostname now
        /// </summary>
        public string IP { get; protected set; }

        public IPEndPoint ResolvedIpEndPoint { get; protected set; }
        public string Version { get; protected set; }
        public bool IsInitialized { get; set; }

        public int Port { get; }
        public abstract Task Kick(string reason, EFClient target, EFClient origin, EFPenalty originalPenalty);

        /// <summary>
        ///     Returns list of all current players
        /// </summary>
        /// <returns></returns>
        public List<EFClient> GetClientsAsList()
        {
            return Clients.FindAll(x => x != null && x.NetworkId != 0);
        }

        /// <summary>
        ///     Add a player to the server's player list
        /// </summary>
        /// <param name="P">EFClient pulled from memory reading</param>
        /// <returns>True if player added successfully, false otherwise</returns>
        public abstract Task<EFClient> OnClientConnected(EFClient P);

        /// <summary>
        ///     Remove player by client number
        /// </summary>
        /// <returns>true if removal succeeded, false otherwise</returns>
        public abstract Task OnClientDisconnected(EFClient client);

        /// <summary>
        ///     Get a player by name
        ///     todo: make this an extension
        /// </summary>
        /// <param name="pName">EFClient name to search for</param>
        /// <returns>Matching player if found</returns>
        public List<EFClient> GetClientByName(string pName)
        {
            if (string.IsNullOrEmpty(pName))
            {
                return new List<EFClient>();
            }

            pName = pName.Trim().StripColors();

            var QuoteSplit = pName.Split('"');
            var literal = false;
            if (QuoteSplit.Length > 1)
            {
                pName = QuoteSplit[1];
                literal = true;
            }

            if (literal)
            {
                return GetClientsAsList().Where(p => p.Name?.StripColors()?.ToLower() == pName.ToLower()).ToList();
            }

            return GetClientsAsList().Where(p => (p.Name?.StripColors()?.ToLower() ?? "").Contains(pName.ToLower()))
                .ToList();
        }

        public virtual Task<bool> ProcessUpdatesAsync(CancellationToken token)
        {
            return (Task<bool>)Task.CompletedTask;
        }

        /// <summary>
        ///     Process any server event
        /// </summary>
        /// <param name="E">Event</param>
        /// <returns>True on sucess</returns>
        protected abstract Task<bool> ProcessEvent(GameEvent E);

        public abstract Task ExecuteEvent(GameEvent E);

        /// <summary>
        ///     Send a message to all players
        /// </summary>
        /// <param name="message">Message to be sent to all players</param>
        /// <param name="sender">Client that initiated the broadcast</param>
        public GameEvent Broadcast(string message, EFClient sender = null)
        {
            var formattedMessage = string.Format(RconParser.Configuration.CommandPrefixes.Say ?? "",
                $"{(CustomSayEnabled && GameName == Game.IW4 ? $"{CustomSayName}: " : "")}{message}");
            ServerLogger.LogDebug("All-> {Message}",
                message.FormatMessageForEngine(RconParser.Configuration).StripColors());

            var e = new GameEvent
            {
                Type = GameEvent.EventType.Broadcast,
                Data = formattedMessage,
                Owner = this,
                Origin = sender
            };

            Manager.AddEvent(e);
            return e;
        }

        [Obsolete("Use BroadcastAsync")]
        public void Broadcast(IEnumerable<string> messages, EFClient sender = null)
        {
            foreach (var message in messages)
            {
#pragma warning disable 4014
                Broadcast(message, sender).WaitAsync();
#pragma warning restore 4014
            }
        }

        public async Task BroadcastAsync(IEnumerable<string> messages, EFClient sender = null,
            CancellationToken token = default)
        {
            foreach (var message in messages)
            {
                if (Manager.CancellationToken.IsCancellationRequested)
                {
                    return;
                }
                
                await Broadcast(message, sender).WaitAsync(Utilities.DefaultCommandTimeout, Manager.CancellationToken);
            }
        }

        /// <summary>
        ///     Send a message to a particular players
        /// </summary>
        /// <param name="message">Message to send</param>
        /// <param name="targetClient">EFClient to send message to</param>
        protected async Task Tell(string message, EFClient targetClient)
        {
            if (!Utilities.IsDevelopment)
            {
                var temporalClientId = targetClient.GetAdditionalProperty<string>("ConnectionClientId");
                var parsedClientId = string.IsNullOrEmpty(temporalClientId) ? (int?)null : int.Parse(temporalClientId);
                var clientNumber = parsedClientId ?? targetClient.ClientNumber;

                var formattedMessage = string.Format(RconParser.Configuration.CommandPrefixes.Tell,
                    clientNumber,
                    $"{(CustomSayEnabled && GameName == Game.IW4 ? $"{CustomSayName}: " : "")}{message}");
                if (targetClient.ClientNumber > -1 && message.Length > 0 &&
                    targetClient.Level != Data.Models.Client.EFClient.Permission.Console)
                {
                    await this.ExecuteCommandAsync(formattedMessage);
                }
            }
            else
            {
                ServerLogger.LogDebug("Tell[{ClientNumber}]->{Message}", targetClient.ClientNumber,
                    message.FormatMessageForEngine(RconParser.Configuration).StripColors());
            }

            if (targetClient.Level == Data.Models.Client.EFClient.Permission.Console)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                var cleanMessage = message.FormatMessageForEngine(RconParser.Configuration)
                    .StripColors();
                using (LogContext.PushProperty("Server", ToString()))
                {
                    ServerLogger.LogInformation("Command output received: {Message}", cleanMessage);
                }

                Console.WriteLine(cleanMessage);
                Console.ForegroundColor = ConsoleColor.Gray;
            }
        }

        /// <summary>
        ///     Send a message to all admins on the server
        /// </summary>
        /// <param name="message">Message to send out</param>
        public void ToAdmins(string message)
        {
            foreach (var client in GetClientsAsList()
                         .Where(c => c.Level > Data.Models.Client.EFClient.Permission.Flagged))
                client.Tell(message);
        }

        /// <summary>
        ///     Kick a player from the server
        /// </summary>
        /// <param name="reason">Reason for kicking</param>
        /// <param name="target">EFClient to kick</param>
        /// <param name="origin">Client initating the kick</param>
        public Task Kick(string reason, EFClient target, EFClient origin)
        {
            return Kick(reason, target, origin, null);
        }

        /// <summary>
        ///     Temporarily ban a player ( default 1 hour ) from the server
        /// </summary>
        /// <param name="reason">Reason for banning the player</param>
        /// <param name="length">Duration of the ban</param>
        /// <param name="target">The client to ban</param>
        /// <param name="origin">The client performing the ban</param>
        public abstract Task TempBan(string reason, TimeSpan length, EFClient target, EFClient origin);

        /// <summary>
        ///     Perm ban a player from the server
        /// </summary>
        /// <param name="reason">The reason for the ban</param>
        /// <param name="target">The person to ban</param>
        /// <param name="origin">The person who banned the target</param>
        /// <param name="isEvade">obsolete</param>
        public abstract Task Ban(string reason, EFClient target, EFClient origin, bool isEvade = false);

        public abstract Task Warn(string reason, EFClient target, EFClient origin);

        /// <summary>
        ///     Unban a player by npID / GUID
        /// </summary>
        /// <param name="reason">reason for unban</param>
        /// <param name="targetClient">client being unbanned</param>
        /// <param name="originClient">client performing the unban</param>
        /// <returns></returns>
        public abstract Task Unban(string reason, EFClient targetClient, EFClient originClient);

        /// <summary>
        ///     Change the current server map
        /// </summary>
        /// <param name="mapName">Non-localized map name</param>
        public async Task LoadMap(string mapName)
        {
            await this.ExecuteCommandAsync($"map {mapName}");
        }

        /// <summary>
        ///     Initalize the macro variables
        /// </summary>
        public abstract void InitializeTokens();

        /// <summary>
        ///     Initialize the messages to be broadcasted
        /// </summary>
        protected void InitializeAutoMessages()
        {
            BroadcastMessages = new List<string>();

            if (ServerConfig.AutoMessages != null)
            {
                BroadcastMessages.AddRange(ServerConfig.AutoMessages);
            }

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
                return (await this.GetDvarAsync("sv_customcallbacks", "0", Manager.CancellationToken)).Value == "1";
            }

            catch (DvarException)
            {
                return false;
            }
        }

        public abstract Task<long> GetIdForServer(Server server = null);

        public string GetServerDvar(string dvarName, int timeoutMs = 1000)
        {
            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(timeoutMs));
            try
            {
                return this.GetDvarAsync<string>(dvarName, token: tokenSource.Token).GetAwaiter().GetResult().Value;
            }
            catch
            {
                return null;
            }
        }

        public bool SetServerDvar(string dvarName, string dvarValue, int timeoutMs = 1000)
        {
            using var tokenSource = new CancellationTokenSource();
            tokenSource.CancelAfter(TimeSpan.FromSeconds(timeoutMs));
            try
            {
                this.SetDvarAsync(dvarName, dvarValue, tokenSource.Token).GetAwaiter().GetResult();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public EFClient GetClientByNumber(int clientNumber) =>
            GetClientsAsList().FirstOrDefault(client => client.ClientNumber == clientNumber);
    }
}
