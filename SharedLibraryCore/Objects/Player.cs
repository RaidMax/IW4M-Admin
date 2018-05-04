using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace SharedLibraryCore.Objects
{
    public class Player : Database.Models.EFClient
    {
        public enum Permission
        {
            Banned = -1,
            User = 0,
            Flagged = 1,
            Trusted = 2,
            Moderator = 3,
            Administrator = 4,
            SeniorAdmin = 5,
            Owner = 6,
            Creator = 7,
            Console = 8,
        }

        public Player()
        {
            ConnectionTime = DateTime.UtcNow;
            ClientNumber = -1;
        }

        public override string ToString()
        {
            return $"{Name}::{NetworkId}";
        }

        public String GetLastConnection()
        {
            return Utilities.GetTimePassed(LastConnection);
        }

        public async Task Tell(String Message)
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
        public int ClientNumber { get; set; }
        [NotMapped]
        public int Ping { get; set; }
        [NotMapped]
        public int Warnings { get; set; }
        [NotMapped]
        public DateTime ConnectionTime { get; set; }
        [NotMapped]
        public Server CurrentServer { get; set; }
        [NotMapped]
        public int Score { get; set; }
        [NotMapped]
        public IList<Dtos.ProfileMeta> Meta { get; set; }
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
            set { _name = value;  }
        }

        public override bool Equals(object obj)
        {
            return ((Player)obj).NetworkId == NetworkId;
        }

        public override int GetHashCode()
        {
            return NetworkId.GetHashCode();
        }
    }
}
