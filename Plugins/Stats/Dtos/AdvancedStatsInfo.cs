using System;
using System.Collections.Generic;
using Data.Models.Client;
using SharedLibraryCore.Dtos;

namespace Stats.Dtos
{
    public class AdvancedStatsInfo
    {
        // Identity
        public long? ServerId { get; set; }
        public string? ServerEndpoint { get; set; }
        public required string ClientName { get; set; }
        public int ClientId { get; set; }
        public EFClient.Permission Level { get; set; }

        // Ranking
        public double? Performance { get; set; }
        public int? Ranking { get; set; }
        public int TotalRankedClients { get; set; }
        public double? ZScore { get; set; }
        public double? Rating { get; set; }

        // Pre-computed aggregate stats
        public int Kills { get; set; }
        public int Deaths { get; set; }
        public string? Kdr { get; set; }
        public int TotalDamage { get; set; }
        public long? Score { get; set; }
        public int? Headshots { get; set; }
        public int? MeleeKills { get; set; }
        public int? Suicides { get; set; }
        public int? UniqueWeapons { get; set; }
        public TimeSpan? ActiveTime { get; set; }

        // Legacy stats (pre-computed)
        public string? Skill { get; set; }
        public string? Elo { get; set; }
        public string? Spm { get; set; }

        // Pre-processed lists (limited to what UI needs)
        public List<ServerInfo> Servers { get; set; } = [];
        public List<WeaponStats> TopWeapons { get; set; } = [];
        public List<HitLocationStats> TopHitLocations { get; set; } = [];
        public List<PerformanceHistory> PerformanceHistory { get; set; } = [];
    }

    /// <summary>
    /// Pre-processed weapon statistics for display
    /// </summary>
    public class WeaponStats
    {
        public required string Name { get; set; }
        public string? AttachmentName { get; set; }
        public int Kills { get; set; }
        public int Hits { get; set; }
        public int Damage { get; set; }
        public int? UsageSeconds { get; set; }
    }

    /// <summary>
    /// Pre-processed hit location statistics for display
    /// </summary>
    public class HitLocationStats
    {
        public required string Name { get; set; }
        public int Hits { get; set; }
        public int Damage { get; set; }
        public float Percentage { get; set; }
    }

    /// <summary>
    /// Performance history data point for charting
    /// </summary>
    public class PerformanceHistory
    {
        public double? Performance { get; set; }
        public DateTime OccurredAt { get; set; }
    }

    /// <summary>
    /// Internal projection for efficient hit stat queries - only loads needed columns
    /// </summary>
    public class HitStatProjection
    {
        public int? HitLocationId { get; set; }
        public string? HitLocationName { get; set; }
        public int? WeaponId { get; set; }
        public string? WeaponName { get; set; }
        public int? MeansOfDeathId { get; set; }
        public string? MeansOfDeathName { get; set; }
        public int? WeaponAttachmentComboId { get; set; }
        public string? Attachment1Name { get; set; }
        public string? Attachment2Name { get; set; }
        public string? Attachment3Name { get; set; }
        public long? ServerId { get; set; }
        public int KillCount { get; set; }
        public int DeathCount { get; set; }
        public int HitCount { get; set; }
        public int DamageInflicted { get; set; }
        public int? Score { get; set; }
        public int? UsageSeconds { get; set; }
    }
}
