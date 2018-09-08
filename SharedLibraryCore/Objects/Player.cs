using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace SharedLibraryCore.Objects
{
    public class Player : Database.Models.EFClient
    {
        public enum ClientState
        {
            /// <summary>
            /// represents when the client has been detected as joining
            /// by the log file, but has not be authenticated by RCon
            /// </summary>
            Connecting,
            /// <summary>
            /// represents when the client has been parsed by RCon, 
            /// but has not been validated against the database
            /// </summary>
            Authenticated,
            /// <summary>
            /// represents when the client has been authenticated by RCon
            /// and validated by the database
            /// </summary>
            Connected,
            /// <summary>
            /// represents when the client is leaving (either through RCon or log file)
            /// </summary>
            Disconnecting,
        }

        public enum Permission
        {
            /// <summary>
            /// client has been banned
            /// </summary>
            Banned = -1,
            /// <summary>
            /// default client state upon first connect
            /// </summary>
            User = 0,
            /// <summary>
            /// client has been flagged
            /// </summary>
            Flagged = 1,
            /// <summary>
            /// client is trusted
            /// </summary>
            Trusted = 2,
            /// <summary>
            /// client is a moderator
            /// </summary>
            Moderator = 3,
            /// <summary>
            /// client is an administrator
            /// </summary>
            Administrator = 4,
            /// <summary>
            /// client is a senior administrator
            /// </summary>
            SeniorAdmin = 5,
            /// <summary>
            /// client is a owner
            /// </summary>
            Owner = 6,
            /// <summary>
            /// not used
            /// </summary>
            Creator = 7,
            /// <summary>
            /// reserved for default account
            /// </summary>
            Console = 8
        }

        public Player()
        {
            ConnectionTime = DateTime.UtcNow;
            ClientNumber = -1;
            DelayedEvents = new Queue<GameEvent>();
            _additionalProperties = new Dictionary<string, object>();
        }

        public override string ToString() => $"{Name}::{NetworkId}";

        public String GetLastConnection()
        {
            return Utilities.GetTimePassed(LastConnection);
        }

        public async Task Tell(String Message)
        {
            // this is console or remote so send immediately
            if (ClientNumber < 0)
            {
                await CurrentServer.Tell(Message, this);
            }

            else
            {
                var e = new GameEvent()
                {
                    Message = Message,
                    Target = this,
                    Owner = CurrentServer,
                    Type = GameEvent.EventType.Tell,
                    Data = Message
                };

                CurrentServer.Manager.GetEventHandler().AddEvent(e);
            }
        }

        public async Task Kick(String Message, Player Sender)
        {
            await CurrentServer.Kick(Message, this, Sender);
        }

        public async Task TempBan(String Message, TimeSpan Length, Player Sender)
        {
            await CurrentServer.TempBan(Message, Length, this, Sender);
        }

        public async Task Warn(String Message, Player Sender)
        {
            await CurrentServer.Warn(Message, this, Sender);
        }

        public async Task Ban(String Message, Player Sender)
        {
            await CurrentServer.Ban(Message, this, Sender);
        }
        [NotMapped]
        Dictionary<string, object> _additionalProperties;
        public T GetAdditionalProperty<T>(string name) => (T)_additionalProperties[name];
        public void SetAdditionalProperty(string name, object value) => _additionalProperties.Add(name, value);
        [NotMapped]
        public int ClientNumber { get; set; }
        [NotMapped]
        public int Ping { get; set; }
        [NotMapped]
        public int Warnings { get; set; }
        [NotMapped]
        public DateTime ConnectionTime { get; set; }
        [NotMapped]
        public int ConnectionLength => (int)(DateTime.UtcNow - ConnectionTime).TotalSeconds;
        [NotMapped]
        public Server CurrentServer { get; set; }
        [NotMapped]
        public int Score { get; set; }
        [NotMapped]
        public bool IsBot { get; set; }
        private int _ipaddress;
        public override int IPAddress
        {
            get { return _ipaddress; }
            set { _ipaddress = value; }
        }
        private string _name;
        public override string Name
        {
            get { return _name; }
            set { _name = value; }
        }
        [NotMapped]
        public ClientState State { get; set; }
        [NotMapped]
        public Queue<GameEvent> DelayedEvents { get; set; }
        [NotMapped]
        // this is kinda dirty, but I need localizable level names
        public ClientPermission ClientPermission => new ClientPermission()
        {
            Level = Level,
            Name = Utilities.CurrentLocalization
                .LocalizationIndex[$"GLOBAL_PERMISSION_{Level.ToString().ToUpper()}"]
        };

        public override bool Equals(object obj)
        {
            return ((Player)obj).NetworkId == NetworkId;
        }

        public override int GetHashCode() => (int)NetworkId;
    }
}
