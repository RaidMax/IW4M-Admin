﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading;
using Data.Models.Server;

namespace Data.Models.Client.Stats
{
    public class EFClientStatistics : SharedEntity
    {
        public EFClientStatistics()
        {
            ProcessingHit = new SemaphoreSlim(1, 1);
        }

        ~EFClientStatistics()
        {
            ProcessingHit.Dispose();
        }

        public int ClientId { get; set; }
        [ForeignKey("ClientId")]
        public virtual EFClient Client { get; set; }
        public long ServerId { get; set; }
        [ForeignKey("ServerId")]
        public virtual EFServer Server { get; set; }
        [Required]
        public int Kills { get; set; }
        [Required]
        public int Deaths { get; set; }
        public double EloRating { get; set; }
        public double ZScore { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public virtual ICollection<EFHitLocationCount> HitLocations { get; set; }
        public double RollingWeightedKDR { get; set; }
        public double AverageSnapValue { get; set; }
        public int SnapHitCount { get; set; }
        [NotMapped]
        public double Performance
        {
            get => Math.Round(EloRating * 1/3.0 + Skill * 2/3.0, 2);
        }
        [NotMapped]
        public double KDR
        {
            get => Deaths == 0 ? Kills : Math.Round(Kills / (double)Deaths, 2);
        }
        [Required]
        public double SPM { get; set; }
        [Required]
        public double Skill { get; set; }
        [Required]
        public int TimePlayed { get; set; }
        [Required]
        public double MaxStrain { get; set; }

        [NotMapped]
        public float AverageHitOffset =>
            (float) Math.Round(
                HitLocations.Sum(c => c.HitOffsetAverage) /
                Math.Max(1, HitLocations.Count(c => c.HitOffsetAverage > 0)), 4);

        [NotMapped]
        public int SessionKills { get; set; }
        [NotMapped]
        public int SessionDeaths { get; set; }
        [NotMapped]
        public int KillStreak { get; set; }
        [NotMapped]
        public int DeathStreak { get; set; }
        [NotMapped]
        public DateTime LastStatCalculation { get; set; }
        [NotMapped]
        public int LastScore { get; set; }
        [NotMapped]
        public DateTime LastActive { get; set; }
        [NotMapped]
        public double MaxSessionStrain { get; set; }
        public void StartNewSession()
        {
            KillStreak = 0;
            DeathStreak = 0;
            LastScore = 0;
            _sessionScores.Add(0);
            Team = 0;
        }
        [NotMapped]
        public int SessionScore
        {
            set => _sessionScores[^1] = value;

            get
            {
                lock (_sessionScores)
                {
                    return new List<int>(_sessionScores).Sum();
                }
            }
        }
        [NotMapped]
        public int RoundScore => _sessionScores[^1];
        [NotMapped]
        private readonly List<int> _sessionScores = new List<int> { 0 };
        [NotMapped]
        public int Team { get; set; }
        [NotMapped]
        public DateTime LastStatHistoryUpdate { get; set; } = DateTime.UtcNow;
        [NotMapped]
        public double SessionSPM { get; set; }
        [NotMapped]
        public SemaphoreSlim ProcessingHit { get; }

        [NotMapped] public MatchData MatchData { get; } = new MatchData();
    }

    public class MatchData
    {
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public double Kdr => Deaths == 0 ? Kills : Math.Round(Kills / (double) Deaths, 2);

        public void StartNewMatch()
        {
            Kills = 0;
            Deaths = 0;
        }
    }
}
