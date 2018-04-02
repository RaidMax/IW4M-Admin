using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using SharedLibrary.RCon;
using SharedLibrary.Commands;
using System.Threading.Tasks;
using SharedLibrary.Helpers;
using SharedLibrary.Objects;
using SharedLibrary.Dtos;
using SharedLibrary.Configuration;

namespace SharedLibrary
{
    public abstract class Server
    {
        public enum Game
        {
            UKN,
            IW3,
            IW4,
            IW5,
            T4,
            T5,
            T5M,
        }

        public Server(Interfaces.IManager mgr, ServerConfiguration config)
        {
            Password = config.Password;
            IP = config.IPAddress;
            Port = config.Port;
            Manager = mgr;
            Logger = Manager.GetLogger();
            ServerConfig = config;
            RemoteConnection = new RCon.Connection(IP, Port, Password, Logger);

            Players = new List<Player>(new Player[18]);
            Reports = new List<Report>();
            PlayerHistory = new Queue<PlayerHistory>();
            ChatHistory = new List<ChatInfo>();
            NextMessage = 0;
            CustomSayEnabled = Manager.GetApplicationSettings().Configuration().EnableCustomSayName;
            CustomSayName = Manager.GetApplicationSettings().Configuration().CustomSayName;
            InitializeTokens();
            InitializeAutoMessages();
        }

        //Returns current server IP set by `net_ip` -- *STRING*
        public String GetIP()
        {
            return IP;
        }

        //Returns current server port set by `net_port` -- *INT*
        public int GetPort()
        {
            return Port;
        }

        //Returns list of all current players
        public List<Player> GetPlayersAsList()
        {
            return Players.FindAll(x => x != null);
        }

        /// <summary>
        /// Add a player to the server's player list
        /// </summary>
        /// <param name="P">Player pulled from memory reading</param>
        /// <returns>True if player added sucessfully, false otherwise</returns>
        abstract public Task<bool> AddPlayer(Player P);

        /// <summary>
        /// Remove player by client number
        /// </summary>
        /// <param name="cNum">Client ID of player to be removed</param>
        /// <returns>true if removal succeded, false otherwise</returns>
        abstract public Task RemovePlayer(int cNum);

        /// <summary>
        /// Get the player from the server's list by line from game long
        /// </summary>
        /// <param name="L">Game log line containing event</param>
        /// <param name="cIDPos">Position in the line where the cliet ID is written</param>
        /// <returns>Matching player if found</returns>
        abstract public Player ParseClientFromString(String[] L, int cIDPos);

        /// <summary>
        /// Get a player by name
        /// </summary>
        /// <param name="pName">Player name to search for</param>
        /// <returns>Matching player if found</returns>
        public List<Player> GetClientByName(String pName)
        {
            string[] QuoteSplit = pName.Split('"');
            bool literal = false;
            if (QuoteSplit.Length > 1)
            {
                pName = QuoteSplit[1];
                literal = true;
            }
            if (literal)
                return Players.Where(p => p != null && p.Name.ToLower().Equals(pName.ToLower())).ToList();

            return Players.Where(p => p != null && p.Name.ToLower().Contains(pName.ToLower())).ToList();
        }

        /// <summary>
        /// Process requested command correlating to an event
        /// </summary>
        /// <param name="E">Event parameter</param>
        /// <param name="C">Command requested from the event</param>
        /// <returns></returns>
        abstract public Task<Command> ValidateCommand(Event E);

        virtual public Task<bool> ProcessUpdatesAsync(CancellationToken cts)
        {
            return null;
        }

        /// <summary>
        /// Process any server event
        /// </summary>
        /// <param name="E">Event</param>
        /// <returns>True on sucess</returns>
        abstract protected Task ProcessEvent(Event E);
        abstract public Task ExecuteEvent(Event E);

        /// <summary>
        /// Send a message to all players
        /// </summary>
        /// <param name="Message">Message to be sent to all players</param>
        public async Task Broadcast(String Message)
        {

            string sayCommand = (GameName == Game.IW4) ? "sayraw" : "say";
#if !DEBUG
            await this.ExecuteCommandAsync($"{sayCommand} {(CustomSayEnabled ? CustomSayName : "")} {Message}");
#else
            Logger.WriteVerbose(Message.StripColors());
#endif
        }

        /// <summary>
        /// Send a message to a particular players
        /// </summary>
        /// <param name="Message">Message to send</param>
        /// <param name="Target">Player to send message to</param>
        public async Task Tell(String Message, Player Target)
        {
            string tellCommand = (GameName == Game.IW4) ? "tellraw" : "tell";

#if !DEBUG
            if (Target.ClientNumber > -1 && Message.Length > 0 && Target.Level != Player.Permission.Console)
                await this.ExecuteCommandAsync($"{tellCommand} {Target.ClientNumber} {(CustomSayEnabled ? CustomSayName : "")} {Message}^7");
#else
            Logger.WriteVerbose($"{Target.ClientNumber}->{Message.StripColors()}");
#endif

            if (Target.Level == Player.Permission.Console)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine(Utilities.StripColors(Message));
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            if (CommandResult.Count > 15)
                CommandResult.RemoveAt(0);
            CommandResult.Add(new CommandResponseInfo()
            {
                Response = Utilities.StripColors(Message),
                ClientId = Target.ClientId
            });
        }

        /// <summary>
        /// Send a message to all admins on the server
        /// </summary>
        /// <param name="message">Message to send out</param>
        public async Task ToAdmins(String message)
        {
            foreach (Player P in Players)
            {
                if (P == null)
                    continue;

                if (P.Level > Player.Permission.Flagged)
                    await P.Tell(message);
            }
        }

        /// <summary>
        /// Kick a player from the server
        /// </summary>
        /// <param name="Reason">Reason for kicking</param>
        /// <param name="Target">Player to kick</param>
        abstract public Task Kick(String Reason, Player Target, Player Origin);

        /// <summary>
        /// Temporarily ban a player ( default 1 hour ) from the server
        /// </summary>
        /// <param name="Reason">Reason for banning the player</param>
        /// <param name="Target">The player to ban</param>
        abstract public Task TempBan(String Reason, TimeSpan length, Player Target, Player Origin);

        /// <summary>
        /// Perm ban a player from the server
        /// </summary>
        /// <param name="Reason">The reason for the ban</param>
        /// <param name="Target">The person to ban</param>
        /// <param name="Origin">The person who banned the target</param>
        abstract public Task Ban(String Reason, Player Target, Player Origin);

        abstract public Task Warn(String Reason, Player Target, Player Origin);

        /// <summary>
        /// Unban a player by npID / GUID
        /// </summary>
        /// <param name="npID">npID of the player</param>
        /// <param name="Target">I don't remember what this is for</param>
        /// <returns></returns>
        abstract public Task Unban(string reason, Player Target, Player Origin);

        /// <summary>
        /// Change the current searver map
        /// </summary>
        /// <param name="mapName">Non-localized map name</param>
        public async Task LoadMap(string mapName)
        {
            await this.ExecuteCommandAsync($"map {mapName}");
        }

        public async Task LoadMap(Map newMap)
        {
            await this.ExecuteCommandAsync($"map {newMap.Name}");
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
        /// todo: this needs to be a serialized file
        /// </summary>
        protected void InitializeAutoMessages()
        {
            BroadcastMessages = new List<String>();

            if(ServerConfig.AutoMessages != null)
                BroadcastMessages.AddRange(ServerConfig.AutoMessages);
            BroadcastMessages.AddRange(Manager.GetApplicationSettings().Configuration().AutoMessages);
        }

        public override string ToString()
        {
            return $"{IP}_{Port}";
        }

        protected async Task<bool> ScriptLoaded()
        {
            try
            {
                return (await this.GetDvarAsync<string>("sv_customcallbacks")).Value == "1";
            }

            catch (Exceptions.DvarException)
            {
                return false;
            }
        }

        // Objects
        public Interfaces.IManager Manager { get; protected set; }
        public Interfaces.ILogger Logger { get; private set; }
        public ServerConfiguration ServerConfig { get; private set; }
        public List<Map> Maps { get; protected set; }
        public List<Report> Reports { get; set; }
        public List<ChatInfo> ChatHistory { get; protected set; }
        public Queue<PlayerHistory> PlayerHistory { get; private set; }
        public Game GameName { get; protected set; }

        // Info
        public string Hostname { get; protected set; }
        public string Website { get; protected set; }
        public string Gametype { get; protected set; }
        public Map CurrentMap { get; protected set; }
        public int ClientNum
        {
            get
            {
                return Players.Where(p => p != null).Count();
            }
        }
        public int MaxClients { get; protected set; }
        public List<Player> Players { get; protected set; }
        public string Password { get; private set; }
        public bool Throttled { get; protected set; }
        public bool CustomCallback { get; protected set; }
        public string WorkingDirectory { get; protected set; }
        public RCon.Connection RemoteConnection { get; protected set; }

        // Internal
        protected string IP;
        protected int Port;
        protected string FSGame;
        protected int NextMessage;
        protected int ConnectionErrors;
        protected List<string> BroadcastMessages;
        protected TimeSpan LastMessage;
        protected IFile LogFile;
        protected DateTime LastPoll;

        // only here for performance
        private bool CustomSayEnabled;
        private string CustomSayName;

        //Remote
        public IList<CommandResponseInfo> CommandResult = new List<CommandResponseInfo>();
    }
}
