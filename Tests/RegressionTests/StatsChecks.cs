using System.Reflection;
using Data.Abstractions;
using Data.Models.Client.Stats;
using Data.Models.Server;
using IW4MAdmin.Plugins.Stats.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using Stats.Config;
using Stats.Helpers;
using Stats.Dtos;

static class StatsChecks
{
    public static async Task Run(IDatabaseContextFactory factory)
    {
        int clientId;
        List<EFServer> serverRows;
        using (var ctx = factory.CreateContext())
        {
            clientId = await ctx.Clients.Select(c => c.ClientId).FirstAsync();
            var a = new EFPerformanceBucket { Code = "audit-a" };
            var b = new EFPerformanceBucket { Code = "audit-b" };
            ctx.PerformanceBuckets.AddRange(a, b);
            await ctx.SaveChangesAsync();
            ctx.Servers.AddRange(
                new EFServer { ServerId = 100, Port = 28960, EndPoint = "127.0.0.1:28960", PerformanceBucketId = a.PerformanceBucketId },
                new EFServer { ServerId = 101, Port = 28961, EndPoint = "127.0.0.1:28961", PerformanceBucketId = b.PerformanceBucketId });
            ctx.ClientStatistics.AddRange(
                new EFClientStatistics { ClientId = clientId, ServerId = 100, Kills = 10, Deaths = 2, TimePlayed = 3600 },
                new EFClientStatistics { ClientId = clientId, ServerId = 101, Kills = 90, Deaths = 2, TimePlayed = 3600 });
            ctx.Set<EFClientHitStatistic>()
                .AddRange(
                    new EFClientHitStatistic { ClientId = clientId, ServerId = 100, KillCount = 10, DeathCount = 2, DamageInflicted = 100 },
                    new EFClientHitStatistic
                        { ClientId = clientId, ServerId = 101, KillCount = 90, DeathCount = 2, DamageInflicted = 900 });
            ctx.Set<EFClientRankingHistory>().Add(new EFClientRankingHistory
            {
                ClientId = clientId, PerformanceBucketId = a.PerformanceBucketId, ServerId = null, Ranking = 0, Newest = true, ZScore = 1,
                PerformanceMetric = 10, CreatedDateTime = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
            serverRows = await ctx.Servers.Include(row => row.PerformanceBucket).ToListAsync();
        }

        var manager = DispatchProxy.Create<IManager, ManagerProxy>();
        var viewer = DispatchProxy.Create<IServerDataViewer, ViewerProxy>();
        ((ManagerProxy)(object)manager).Servers = serverRows.Select(row => (Server)Check.Server(row)).ToList();
        var cache = new Data.Helpers.LookupCache<EFServer>(NullLogger<Data.Helpers.LookupCache<EFServer>>.Instance, factory);
        await cache.InitializeAsync();
        var statManager = new StatManager(NullLogger<StatManager>.Instance, factory,
            new StatsConfiguration { EnableAdvancedMetrics = true, TopPlayersMinPlayTime = 0 }, null!, cache);
        IW4MAdmin.Plugins.Stats.Plugin.ServerManager = manager;
        Utilities.CurrentLocalization = new SharedLibraryCore.Localization.Layout(new Dictionary<string, string>())
            { LocalizationName = "en-US" };
        var result = await statManager.GetNewTopStats(0, 25, performanceBucketCode: "audit-a");
        Check.Equal(10, result.Players.Single().Kills, "Category leaderboard totals");
        var helper = new AdvancedClientStatsResourceQueryHelper(NullLogger<AdvancedClientStatsResourceQueryHelper>.Instance, factory,
            manager, new DefaultSettings { GameStrings = new GameStringConfiguration() }, viewer, statManager);
        var advanced = await helper.QueryResource(new StatsInfoRequest { ClientId = clientId, PerformanceBucketCode = "audit-a" });
        Check.Equal(10, advanced.Results.Single().Kills, "Category advanced totals");
        using (var context = factory.CreateContext())
        {
            var a = await context.PerformanceBuckets.SingleAsync(bucket => bucket.Code == "audit-a");
            context.Set<EFClientRankingHistory>().Add(new EFClientRankingHistory
            {
                ClientId = clientId, ServerId = 100,
                PerformanceBucketId = a.PerformanceBucketId, Ranking = 2, Newest = true, ZScore = 1, PerformanceMetric = 10,
                CreatedDateTime = DateTime.UtcNow
            });
            var otherServer = new EFServer
                { ServerId = 102, Port = 28962, EndPoint = "audit-a-2", PerformanceBucketId = a.PerformanceBucketId };
            context.Servers.Add(otherServer);
            context.ClientStatistics.Add(new EFClientStatistics
                { ClientId = clientId, ServerId = 102, Kills = 5, Deaths = 1, TimePlayed = 3600 });
            var weapon = new Data.Models.Client.Stats.Reference.EFWeapon { Name = "rifle", Game = Data.Models.Reference.Game.T5 };
            var location = new Data.Models.Client.Stats.Reference.EFHitLocation { Name = "torso", Game = Data.Models.Reference.Game.T5 };
            context.AddRange(weapon, location);
            await context.SaveChangesAsync();
            context.Set<EFClientHitStatistic>().Add(new EFClientHitStatistic
                { ClientId = clientId, ServerId = 102, KillCount = 5, DamageInflicted = 50 });
            foreach (var serverId in new long[] { 100, 102 })
            {
                context.Set<EFClientHitStatistic>().Add(new EFClientHitStatistic
                {
                    ClientId = clientId, ServerId = serverId, WeaponId = weapon.WeaponId, KillCount = 3, HitCount = 6, DamageInflicted = 60
                });
                context.Set<EFClientHitStatistic>().Add(new EFClientHitStatistic
                {
                    ClientId = clientId, ServerId = serverId, HitLocationId = location.HitLocationId, HitCount = 6, DamageInflicted = 60
                });
            }

            await context.SaveChangesAsync();
        }

        Check.Equal(3, await statManager.GetClientOverallRanking(clientId, 100), "Server resolves named ranking category");
        var serverEndpoint = ((ManagerProxy)(object)manager).Servers.First().Id;
        var serverStats = (await helper.QueryResource(new StatsInfoRequest { ClientId = clientId, ServerEndpoint = serverEndpoint }))
            .Results.Single();
        Check.Equal("audit-a", serverStats.PerformanceBucket, "Server advanced category");
        Check.Equal(3, serverStats.Ranking, "Server advanced ranking history");
        Check.Equal("audit-a", serverStats.Servers.First().PerformanceBucket, "Advanced category navigation");
        var totals = (await helper.QueryResource(new StatsInfoRequest { ClientId = clientId, PerformanceBucketCode = "audit-a" })).Results
            .Single();
        Check.Equal(15, totals.Kills, "Multiple-server category summary");
        Check.Equal(6, totals.TopWeapons.Single().Kills, "Multiple-server category weapon totals");
        Check.Equal(12, totals.TopHitLocations.Single().Hits, "Multiple-server hit-location totals");
    }
}

public class ManagerProxy : DispatchProxy
{
    public List<Server> Servers { get; set; } = new();

    protected override object? Invoke(MethodInfo? method, object?[]? args) => method!.Name switch
    {
        "GetServers" => Servers,
        "get_CustomTopStatsTransformers" => new List<Func<IList<ITopStatsMutable>, long?, string, Task>>(),
        "get_CustomStatsMetrics" => new List<Func<Dictionary<int, List<Data.Models.EFMeta>>, long?, string, bool, Task>>(),
        _ => method.ReturnType.IsValueType ? Activator.CreateInstance(method.ReturnType) : null
    };
}

public class ViewerProxy : DispatchProxy
{
    protected override object? Invoke(MethodInfo? method, object?[]? args) =>
        method!.Name == "RankedClientsCountAsync" ? Task.FromResult(1) : null;
}
