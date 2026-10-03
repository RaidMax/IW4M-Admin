using Data.Context;
using Data.Models;
using Data.Models.Client;
using Data.Models.Server;
using Data.Models.Zombie;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace ZombieStats.Tests;

public sealed class DatabaseTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private Factory _factory = null!;
    private int _clientId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _factory = new Factory(new DbContextOptionsBuilder().UseSqlite(_connection).Options);
        await using var context = _factory.CreateContext();
        await context.Database.MigrateAsync();
        var client = new EFClient { NetworkId = 998877, GameName = Reference.Game.T5,
            CurrentAlias = new EFAlias { Name = "Test", Link = new EFAliasLink() }, AliasLink = new EFAliasLink() };
        context.Clients.Add(client);
        context.Servers.AddRange(new EFServer { ServerId = 1, Port = 28960 }, new EFServer { ServerId = 2, Port = 28961 });
        await context.SaveChangesAsync();
        _clientId = client.ClientId;
    }

    [Theory]
    [InlineData(null)] [InlineData(1L)]
    public async Task Aggregate_dedupe_rejects_duplicate_lifetime_and_per_server_rows(long? serverId)
    {
        await using var context = _factory.CreateContext();
        context.ZombieClientStatAggregates.Add(new ZombieAggregateClientStat { ClientId = _clientId, ServerId = serverId });
        await context.SaveChangesAsync();
        context.ZombieClientStatAggregates.Add(new ZombieAggregateClientStat { ClientId = _clientId, ServerId = serverId });
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(19, Assert.IsType<SqliteException>(error.InnerException).SqliteErrorCode);
    }

    [Fact]
    public async Task Lifetime_and_separate_server_aggregates_can_coexist_and_dedupe_updates_with_the_row()
    {
        await using var context = _factory.CreateContext();
        var lifetime = new ZombieAggregateClientStat { ClientId = _clientId };
        var perServer = new ZombieAggregateClientStat { ClientId = _clientId, ServerId = 1 };
        context.ZombieClientStatAggregates.AddRange(lifetime, perServer);
        await context.SaveChangesAsync();
        perServer.ServerId = 2;
        await context.SaveChangesAsync();
        context.ZombieClientStatAggregates.Add(new ZombieAggregateClientStat { ClientId = _clientId, ServerId = 1 });
        await context.SaveChangesAsync();
        Assert.Equal(3, await context.ZombieClientStatAggregates.CountAsync());
        Assert.Equal(-1L, context.Entry(lifetime).Property<long>(DatabaseContext.ZombieAggregateDedupeServerIdColumn).CurrentValue);
        Assert.Equal(2L, context.Entry(perServer).Property<long>(DatabaseContext.ZombieAggregateDedupeServerIdColumn).CurrentValue);
    }

    [Fact]
    public async Task Scoring_migration_backfills_each_clients_attendance_without_crossing_matches()
    {
        await using var context = _factory.CreateContext();
        var first = new ZombieMatch { HighestRound = 10, MatchStartDate = DateTimeOffset.UtcNow };
        var second = new ZombieMatch { HighestRound = 50, MatchStartDate = DateTimeOffset.UtcNow };
        context.ZombieMatches.AddRange(first, second);
        await context.SaveChangesAsync();
        context.ZombieMatchClientStats.AddRange(new ZombieMatchClientStat { ClientId = _clientId, MatchId = first.ZombieMatchId },
            new ZombieMatchClientStat { ClientId = _clientId, MatchId = second.ZombieMatchId });
        context.ZombieRoundClientStats.AddRange(new ZombieRoundClientStat { ClientId = _clientId, MatchId = first.ZombieMatchId, RoundNumber = 3 },
            new ZombieRoundClientStat { ClientId = _clientId, MatchId = first.ZombieMatchId, RoundNumber = 10 },
            new ZombieRoundClientStat { ClientId = _clientId, MatchId = second.ZombieMatchId, RoundNumber = 40 },
            new ZombieRoundClientStat { ClientId = _clientId, MatchId = second.ZombieMatchId, RoundNumber = 50 });
        await context.SaveChangesAsync();
        var migrator = context.GetService<IMigrator>();
        var previous = context.Database.GetMigrations().TakeWhile(m => !m.EndsWith("PersistZombieRoundScoring")).Last();
        await migrator.MigrateAsync(previous);
        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();
        var rows = await context.ZombieMatchClientStats.OrderBy(r => r.MatchId).ToListAsync();
        Assert.Equal(3, rows[0].JoinedRound); Assert.Equal(10, rows[0].LastRoundReached);
        Assert.Equal(40, rows[1].JoinedRound); Assert.Equal(50, rows[1].LastRoundReached);
        Assert.All(await context.ZombieRoundClientStats.ToListAsync(), round =>
        { Assert.Null(round.RelativeSpeed); Assert.Null(round.AggregatesApplied); });
    }

    [Fact]
    public async Task Date_time_offsets_roundtrip_and_sql_ordering_follow_chronological_instants()
    {
        await using var context = _factory.CreateContext();
        var earlier = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));
        var later = new DateTimeOffset(2026, 1, 1, 11, 0, 0, TimeSpan.Zero);
        context.ZombieMatches.AddRange(new ZombieMatch { MatchStartDate = later }, new ZombieMatch { MatchStartDate = earlier });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var dates = await context.ZombieMatches.OrderBy(m => m.MatchStartDate).Select(m => m.MatchStartDate).ToListAsync();
        Assert.Equal(new[] { earlier, later }, dates);
        Assert.Single(await context.ZombieMatches.Where(m => m.MatchStartDate >= later).ToListAsync());
        Assert.All(dates, date => Assert.Equal(TimeSpan.Zero, date.Offset));
        Assert.Single(await context.ZombieMatches.Where(m => m.MatchStartDate == earlier.ToUniversalTime()).ToListAsync());
    }

    [Theory]
    [InlineData(330)] [InlineData(-90)] [InlineData(840)] [InlineData(-840)] [InlineData(0)]
    public async Task Utc_migration_preserves_existing_binary_instants_and_nullable_values(int offsetMinutes)
    {
        await using var context = _factory.CreateContext();
        var migrator = context.GetService<IMigrator>();
        var previous = context.Database.GetMigrations().TakeWhile(m => !m.EndsWith("NormalizeZombieTimestampsToUtc")).Last();
        await migrator.MigrateAsync(previous);
        var instant = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromMinutes(offsetMinutes)).AddMilliseconds(123);
        var match = new ZombieMatch { MatchStartDate = DateTimeOffset.UtcNow };
        context.ZombieMatches.Add(match);
        await context.SaveChangesAsync();
        var binary = DateTimeOffsetToBinaryConverter.ToLong(instant);
        await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE EFZombieMatches SET MatchStartDate={binary}, CreatedDateTime={binary}, UpdatedDateTime={binary}, EasterEggOccurredAt={binary}, MatchEndDate=NULL WHERE ZombieMatchId={match.ZombieMatchId}");
        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();
        var restored = await context.ZombieMatches.SingleAsync();
        Assert.Equal(instant, restored.MatchStartDate);
        Assert.Equal(TimeSpan.Zero, restored.MatchStartDate.Offset);
        Assert.Equal(instant, restored.CreatedDateTime);
        Assert.Equal(instant, restored.UpdatedDateTime);
        Assert.Equal(instant, restored.EasterEggOccurredAt);
        Assert.Equal(TimeSpan.Zero, restored.EasterEggOccurredAt!.Value.Offset);
        Assert.Null(restored.MatchEndDate);
        Assert.Single(await context.ZombieMatches.Where(m => m.MatchStartDate == instant).ToListAsync());
        // Normalization is safe on rollback/reapply and never applies the old offset twice.
        await migrator.MigrateAsync(previous);
        await migrator.MigrateAsync();
        context.ChangeTracker.Clear();
        Assert.Equal(instant, (await context.ZombieMatches.SingleAsync()).MatchStartDate);
    }

    public Task DisposeAsync() => _connection.DisposeAsync().AsTask();
}
