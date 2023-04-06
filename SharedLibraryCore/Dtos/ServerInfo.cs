using System;
using System.Collections.Generic;
using System.Linq;
using Data.Models;
using SharedLibraryCore.Helpers;

namespace SharedLibraryCore.Dtos
{
    public class ServerInfo
    {
        public string Name { get; set; }
        public int Port { get; set; }
        public string Map { get; set; }
        public string GameType { get; set; }
        public int ClientCount { get; set; }
        public int MaxClients { get; set; }
        public int PrivateClientSlots { get; set; }
        public List<ChatInfo> ChatHistory { get; set; }
        public List<PlayerInfo> Players { get; set; }
        public List<Report> Reports { get; set; }
        public ClientHistoryInfo ClientHistory { get; set; }
        public long ID { get; set; }
        public bool Online { get; set; }
        public string ConnectProtocolUrl { get; set; }
        public string IPAddress { get; set; }
        public string ExternalIPAddress { get; set; }
        public bool IsPasswordProtected { get; set; }
        public string Endpoint => $"{IPAddress}:{Port}";

        public double? LobbyZScore
        {
            get
            {
                var valid = Players.Where(player => player.ZScore != null && player.ZScore != 0)
                    .ToList();

                if (!valid.Any())
                {
                    return null;
                }

                return Math.Round(valid.Select(player => player.ZScore.Value).Average(), 2);
            }
        }
        public Reference.Game Game { get; set; }
    }
}
