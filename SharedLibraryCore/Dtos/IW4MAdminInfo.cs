using static SharedLibraryCore.Server;

namespace SharedLibraryCore.Dtos
{
    public class IW4MAdminInfo
    {
        public int TotalClientCount { get; set; }
        public int RecentClientCount { get; set; }
        public int TotalOccupiedClientSlots { get; set; }
        public int TotalAvailableClientSlots { get; set; }

        /// <summary>
        /// specifies the game name filter
        /// </summary>
        public Game? Game { get; set; }

        /// <summary>
        /// collection of unique game names being monitored
        /// </summary>
        public Game[] ActiveServerGames { get; set; }
    }
}
