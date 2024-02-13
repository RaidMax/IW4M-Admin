﻿using SharedLibraryCore.Dtos;
using System;
using System.Collections.Generic;
using Data.Models;

namespace IW4MAdmin.Plugins.Stats.Web.Dtos
{
    public class TopStatsInfo : SharedInfo
    {
        public int Ranking { get; set; }
        public string Name { get; set; }
        public int ClientId { get; set; }
        public double KDR { get; set; }
        public double? Performance { get; set; }
        public string TimePlayed { get; set; }
        public TimeSpan TimePlayedValue { get; set; }
        public string LastSeen { get; set; }
        public TimeSpan LastSeenValue { get; set; }
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public int RatingChange { get; set; }
        public List<PerformanceHistory> PerformanceHistory { get; set; }
        public double? ZScore { get; set; }
        public long? ServerId { get; set; }
        public List<EFMeta> Metrics { get; } = new();
    }

    public class PerformanceHistory
    {
        public double? Performance { get; set; }
        public DateTime OccurredAt { get; set; }
    }
}
