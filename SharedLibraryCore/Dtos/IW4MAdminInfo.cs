using System;
using static SharedLibraryCore.Server;

namespace SharedLibraryCore.Dtos
{
    public class IW4MAdminInfo
    {
        public int TotalClientCount { get; set; }
        public int RecentClientCount { get; set; }
        public int TotalOccupiedClientSlots { get; set; }
        public int TotalAvailableClientSlots { get; set; }
        public int MaxConcurrentClients { get; set; }
        public DateTime MaxConcurrentClientsTime { get; set; }

        /// <summary>
        ///     specifies the game name filter
        /// </summary>
        public Game? Game { get; set; }

        /// <summary>
        ///     collection of unique game names being monitored
        /// </summary>
        public Game[] ActiveServerGames { get; set; }
    }
}