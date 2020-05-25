using System;
using System.Text.Json.Serialization;

namespace Stats.Dtos
{
    public class StatsInfoResult
    {
        /// <summary>
        /// ranking on the server
        /// </summary>
        public int Ranking { get; set; }

        /// <summary>
        /// number of kills
        /// </summary>
        public int Kills { get; set; }

        /// <summary>
        /// number of deaths
        /// </summary>
        public int Deaths { get; set; }

        /// <summary>
        /// performance level (elo rating + skill) / 2
        /// </summary>
        public double Performance { get; set; }
        
        /// <summary>
        /// SPM
        /// </summary>
        public double ScorePerMinute { get; set; }

        /// <summary>
        /// last connection
        /// </summary>
        public DateTime LastPlayed { get; set; }

        /// <summary>
        /// how many seconds played on the server
        /// </summary>
        public double TotalSecondsPlayed { get; set; }

        /// <summary>
        /// name of the server
        /// </summary>
        public string ServerName { get; set; }

        /// <summary>
        /// server game
        /// </summary>
        public string ServerGame { get; set; }

        [JsonIgnore]
        public long ServerId { get; set; }
    }
}
