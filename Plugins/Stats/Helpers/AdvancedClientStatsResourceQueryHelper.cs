using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client;
using Data.Models.Client.Stats;
using IW4MAdmin.Plugins.Stats;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using Stats.Dtos;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Stats.Helpers
{
    public class AdvancedClientStatsResourceQueryHelper(
        ILogger<AdvancedClientStatsResourceQueryHelper> logger,
        IDatabaseContextFactory contextFactory,
        IManager manager,
        DefaultSettings defaultSettings)
        : IResourceQueryHelper<StatsInfoRequest, AdvancedStatsInfo>
    {
        private readonly ILogger _logger = logger;

        public async Task<ResourceQueryHelperResult<AdvancedStatsInfo>> QueryResource(StatsInfoRequest query)
        {
            await using var context = contextFactory.CreateContext(enableTracking: false);

            long? serverId = null;

            if (!string.IsNullOrEmpty(query.ServerEndpoint))
            {
                serverId = (await context.Servers
                        .Select(server => new { server.EndPoint, server.Id })
                        .FirstOrDefaultAsync(server => server.EndPoint == query.ServerEndpoint))
                    ?.Id;
            }

            var clientInfo = await context.Clients.Select(client => new
            {
                client.ClientId,
                client.CurrentAlias.Name,
                client.Level,
                client.GameName
            }).FirstOrDefaultAsync(client => client.ClientId == query.ClientId);

            if (clientInfo == null)
            {
                return new ResourceQueryHelperResult<AdvancedStatsInfo>();
            }

            var hitStats = await context.Set<EFClientHitStatistic>()
                .Where(stat => stat.ClientId == query.ClientId)
                .Where(stat => stat.ServerId == serverId)
                .Select(stat => new HitStatProjection
                {
                    HitLocationId = stat.HitLocationId,
                    HitLocationName = stat.HitLocation != null ? stat.HitLocation.Name : null,
                    WeaponId = stat.WeaponId,
                    WeaponName = stat.Weapon != null ? stat.Weapon.Name : null,
                    MeansOfDeathId = stat.MeansOfDeathId,
                    MeansOfDeathName = stat.MeansOfDeath != null ? stat.MeansOfDeath.Name : null,
                    WeaponAttachmentComboId = stat.WeaponAttachmentComboId,
                    Attachment1Name = stat.WeaponAttachmentCombo != null
                        ? stat.WeaponAttachmentCombo.Attachment1.Name
                        : null,
                    Attachment2Name =
                        stat.WeaponAttachmentCombo != null && stat.WeaponAttachmentCombo.Attachment2 != null
                            ? stat.WeaponAttachmentCombo.Attachment2.Name
                            : null,
                    Attachment3Name =
                        stat.WeaponAttachmentCombo != null && stat.WeaponAttachmentCombo.Attachment3 != null
                            ? stat.WeaponAttachmentCombo.Attachment3.Name
                            : null,
                    ServerId = stat.ServerId,
                    KillCount = stat.KillCount,
                    DeathCount = stat.DeathCount,
                    HitCount = stat.HitCount,
                    DamageInflicted = stat.DamageInflicted,
                    Score = stat.Score,
                    UsageSeconds = stat.UsageSeconds,
                    GameName = stat.Weapon != null ? stat.Weapon.Game : null
                })
                .ToListAsync();

            var ratings = await context.Set<EFClientRankingHistory>()
                .Where(r => r.ClientId == clientInfo.ClientId)
                .Where(r => r.ServerId == serverId)
                .Where(r => r.Ranking != null)
                .OrderByDescending(r => r.CreatedDateTime)
                .Take(100)
                .Select(r => new { r.Newest, r.PerformanceMetric, r.ZScore, r.CreatedDateTime, r.Ranking })
                .ToListAsync();

            var legacyStats = await context.Set<EFClientStatistics>()
                .Where(stat => stat.ClientId == query.ClientId)
                .Where(stat => serverId == null || stat.ServerId == serverId)
                .Select(stat => new { stat.ServerId, stat.Skill, stat.EloRating, stat.SPM, stat.TimePlayed })
                .ToListAsync();

            var mostRecentRanking = ratings.FirstOrDefault(ranking => ranking.Newest);
            var ranking = mostRecentRanking?.Ranking + 1;

            if (mostRecentRanking != null && mostRecentRanking.CreatedDateTime < Extensions.FifteenDaysAgo())
            {
                ranking = 0;
            }

            if (clientInfo.Level == EFClient.Permission.Banned)
            {
                ranking = null;
            }

            // Single-pass categorization using projection results
            HitStatProjection? aggregate = null;
            var byHitLocation = new List<HitStatProjection>();
            var byWeapon = new List<HitStatProjection>();

            // Keys for special stats
            const string headshotKey = "MOD_HEAD_SHOT";
            const string headshotKey2 = "headshot";
            const string meleeKey = "MOD_MELEE";
            var suicideKeys = new[] { "MOD_SUICIDE", "MOD_FALLING" };

            int? headshots = null;
            int? meleeKills = null;
            int? suicides = null;
            long? score = null;
            var hasPerServerData = false;

            foreach (var hit in hitStats)
            {
                hasPerServerData = true;

                // Accumulate per-server special stats
                if (hit.MeansOfDeathName == headshotKey || hit.HitLocationName == headshotKey2)
                {
                    headshots = (headshots ?? 0) + hit.HitCount;
                }

                if (hit.MeansOfDeathName == meleeKey)
                {
                    meleeKills = (meleeKills ?? 0) + hit.KillCount;
                }

                if (suicideKeys.Contains(hit.MeansOfDeathName ?? ""))
                {
                    suicides = (suicides ?? 0) + hit.KillCount;
                }

                score = (score ?? 0) + (hit.Score ?? 0);

                // Check for aggregate record
                if (hit.HitLocationId == null && hit.ServerId == serverId &&
                    hit.WeaponId == null && hit.MeansOfDeathId == null)
                {
                    aggregate = hit;
                }

                // Categorize by hit location (no weapon, no attachment combo)
                if (hit is { HitLocationId: not null, WeaponId: null, WeaponAttachmentComboId: null })
                {
                    byHitLocation.Add(hit);
                }

                // Categorize by weapon (no hit location)
                if (hit.HitLocationId == null && hit.WeaponId != null)
                {
                    byWeapon.Add(hit);
                }
            }

            // Process weapons: group by weapon, find most used attachment
            var topWeapons = byWeapon
                .Where(hit => hit.DamageInflicted > 0 || hit is { DamageInflicted: 0, HitCount: > 0 })
                .GroupBy(hit => hit.WeaponId)
                .Select(group =>
                {
                    var primary = group.FirstOrDefault(hit => hit.WeaponAttachmentComboId == null) ?? group.First();
                    var mostUsedAttachment = group
                        .Where(g => g.WeaponAttachmentComboId != null)
                        .OrderByDescending(g => g.DamageInflicted)
                        .FirstOrDefault();

                    return new WeaponStats
                    {
                        Name = GetWeaponNameForHit(primary),
                        AttachmentName = mostUsedAttachment != null
                            ? BuildAttachmentNameFromProjection(mostUsedAttachment)
                            : null,
                        Kills = primary.KillCount,
                        Hits = primary.HitCount,
                        Damage = primary.DamageInflicted,
                        UsageSeconds = primary.UsageSeconds
                    };
                })
                .OrderByDescending(w => w.Kills)
                .ToList();

            // Process hit locations: filter, sort, limit to 5
            var filteredHitLocations = byHitLocation
                .Where(hit => hit.HitCount > 0)
                .Where(hit => hit.HitLocationName != "none")
                .Where(hit => hit.HitLocationName != "neck")
                .OrderByDescending(hit => hit.HitCount)
                .ThenBy(hit => hit.HitLocationId)
                .ToList();

            var totalHits = filteredHitLocations.Sum(h => h.HitCount);
            var topHitLocations = filteredHitLocations
                .Select(hit => new HitLocationStats
                {
                    Name = defaultSettings.GameStrings.GetStringForGame(hit.HitLocationName ?? "Unknown",
                        hit.GameName ?? Reference.Game.IW4),
                    InternalName = hit.HitLocationName ?? "unknown",
                    Hits = hit.HitCount,
                    Damage = hit.DamageInflicted,
                    Percentage = totalHits > 0 ? (float)hit.HitCount / totalHits : 0
                })
                .ToList();

            // Process performance history
            var performanceHistory = ratings
                .OrderBy(r => r.CreatedDateTime)
                .Select(r => new PerformanceHistory
                {
                    Performance = r.PerformanceMetric,
                    OccurredAt = r.CreatedDateTime
                })
                .ToList();

            // Add current performance if different from most recent
            var performance = mostRecentRanking?.PerformanceMetric;
            if (performance != null && performance != ratings.FirstOrDefault()?.PerformanceMetric)
            {
                performanceHistory.Add(new PerformanceHistory
                {
                    Performance = performance.Value,
                    OccurredAt = ratings.FirstOrDefault()?.CreatedDateTime ?? DateTime.UtcNow
                });
            }

            // Extract legacy stats values - compute weighted averages manually since we have projection
            var serverLegacyStat = legacyStats.FirstOrDefault(stat => stat.ServerId == serverId);
            string? skill, elo, spm;

            if (serverId != null)
            {
                skill = serverLegacyStat?.Skill.ToNumericalString();
                elo = serverLegacyStat?.EloRating.ToNumericalString();
                spm = serverLegacyStat?.SPM.ToNumericalString();
            }
            else
            {
                // Weighted average by playtime
                var totalPlaytime = legacyStats.Sum(s => s.TimePlayed);
                if (totalPlaytime > 0)
                {
                    skill = legacyStats.Sum(s => s.Skill * s.TimePlayed / totalPlaytime).ToNumericalString();
                    elo = legacyStats.Sum(s => s.EloRating * s.TimePlayed / totalPlaytime).ToNumericalString();
                    spm = legacyStats.Sum(s => s.SPM * s.TimePlayed / totalPlaytime).ToNumericalString();
                }
                else
                {
                    skill = null;
                    elo = null;
                    spm = null;
                }
            }

            // Compute KDR
            var kills = aggregate?.KillCount ?? 0;
            var deaths = aggregate?.DeathCount ?? 0;
            var kdr = deaths > 0
                ? Math.Round(kills / (float)deaths, 2).ToString(Utilities.CurrentLocalization.Culture)
                : null;

            // Unique weapons count
            var uniqueWeapons = hasPerServerData
                ? (int?)byWeapon.Where(w => w.DamageInflicted > 0).GroupBy(w => w.WeaponId).Count()
                : null;

            // Active time
            var activeTime = topWeapons.Count != 0
                ? TimeSpan.FromSeconds(topWeapons.Sum(w => w.UsageSeconds ?? 0))
                : (TimeSpan?)null;

            var hitInfo = new AdvancedStatsInfo
            {
                // Identity
                ServerId = serverId,
                ServerEndpoint = query.ServerEndpoint,
                ClientName = clientInfo.Name,
                ClientId = clientInfo.ClientId,
                Level = clientInfo.Level,

                // Ranking
                Performance = performance,
                ZScore = mostRecentRanking?.ZScore,
                Rating = mostRecentRanking?.PerformanceMetric,
                Ranking = ranking,

                // Aggregate stats
                Kills = kills,
                Deaths = deaths,
                Kdr = kdr,
                TotalDamage = aggregate?.DamageInflicted ?? 0,
                Score = hasPerServerData ? score : null,
                Headshots = headshots,
                MeleeKills = meleeKills,
                Suicides = suicides,
                UniqueWeapons = uniqueWeapons,
                ActiveTime = activeTime,

                // Legacy stats
                Skill = skill,
                Elo = elo,
                Spm = spm,

                // Pre-processed lists
                Servers = manager.GetServers()
                    .Select(server => new ServerInfo
                    {
                        Name = server.Hostname,
                        IPAddress = server.ListenAddress,
                        Port = server.ListenPort,
                        Game = (Reference.Game)server.GameName
                    })
                    .Where(server => server.Game == clientInfo.GameName)
                    .ToList(),
                TopWeapons = topWeapons,
                TopHitLocations = topHitLocations,
                PerformanceHistory = performanceHistory
            };

            return new ResourceQueryHelperResult<AdvancedStatsInfo>
            {
                Results = [hitInfo]
            };
        }

        private string? BuildAttachmentNameFromProjection(HitStatProjection? proj)
        {
            if (proj == null)
                return null;
            var parts = new[]
                {
                    defaultSettings.GameStrings.GetStringForGame(proj.Attachment1Name),
                    defaultSettings.GameStrings.GetStringForGame(proj.Attachment2Name),
                    defaultSettings.GameStrings.GetStringForGame(proj.Attachment3Name)
                }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(" + ", parts);
        }

        private string GetWeaponNameForHit(HitStatProjection? proj)
        {
            if (proj is null)
            {
                return "Unknown";
            }

            var rebuiltName = RebuildWeaponName(proj);
            var name = defaultSettings.GameStrings.GetStringForGame(rebuiltName, proj.GameName);
            return !rebuiltName.Equals(name, StringComparison.InvariantCultureIgnoreCase)
                ? name
                : defaultSettings.GameStrings.GetStringForGame(proj.WeaponName, proj.GameName);
        }

        private static string RebuildWeaponName(HitStatProjection? proj)
        {
            return proj is null
                ? "Unknown"
                : $"{proj.WeaponName}{string.Join("_", proj.Attachment1Name, proj.Attachment2Name, proj.Attachment3Name)}";
        }

        public static Expression<Func<EFClientStatistics, bool>> GetRankingFunc(int minPlayTime, double? zScore = null,
            long? serverId = null)
        {
            return stats => (serverId == null || stats.ServerId == serverId) &&
                            stats.UpdatedAt >= Extensions.FifteenDaysAgo() &&
                            stats.Client.Level != EFClient.Permission.Banned &&
                            stats.TimePlayed >= minPlayTime
                            && (zScore == null || stats.ZScore > zScore);
        }
    }
}
