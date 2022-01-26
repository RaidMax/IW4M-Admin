using System;
using SharedLibraryCore.Dtos;

namespace SharedLibraryCore.Helpers
{
    public class PlayerHistory
    {
        // how many minutes between updates
        public static readonly int UpdateInterval = 5;

        private readonly DateTime When;

        public PlayerHistory(int cNum)
        {
            var t = DateTime.UtcNow;
            When = new DateTime(t.Year, t.Month, t.Day, t.Hour,
                Math.Min(59, UpdateInterval * (int)Math.Round(t.Minute / (float)UpdateInterval)), 0);
            y = cNum;
        }

        /// <summary>
        ///     Used by CanvasJS as a point on the x axis
        /// </summary>
        public string x => When.ToString("yyyy-MM-ddTHH:mm:ssZ");

        /// <summary>
        ///     Used by CanvasJS as a point on the y axis
        /// </summary>
        public int y { get; }

        public ClientCountSnapshot ToClientCountSnapshot()
        {
            return new ClientCountSnapshot
            {
                ClientCount = y,
                Time = When
            };
        }
    }
}