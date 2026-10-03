using System.Net;
using System.Reflection;
using Data.Models;
using Data.Models.Client;
using Data.Models.Zombie;
using IW4MAdmin.Application.Plugin;
using IW4MAdmin.Application.Plugin.CSharpScript;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SharedLibraryCore.Configuration;

if (args is ["--package", var packageDirectory])
{
    await PackagedCompilerChecks.Run(packageDirectory);
    return;
}

var previousLocalization = SharedLibraryCore.Utilities.CurrentLocalization;
SharedLibraryCore.Utilities.CurrentLocalization = new SharedLibraryCore.Localization.Layout(new Dictionary<string, string>
{
    ["GLOBAL_TIME_MINUTES"] = "minutes", ["GLOBAL_TIME_HOURS"] = "hours", ["GLOBAL_TIME_DAYS"] = "days",
    ["GLOBAL_TIME_WEEKS"] = "weeks", ["GLOBAL_TIME_YEARS"] = "years"
});
try
{
    Check.Equal(TimeSpan.FromMinutes(30), SharedLibraryCore.Utilities.ParseTimespan("30m"), "Temp-ban length parses minutes");
    Check.Equal(TimeSpan.FromDays(14), SharedLibraryCore.Utilities.ParseTimespan("2w"), "Temp-ban length parses weeks");
    Check.Equal(TimeSpan.FromHours(1), SharedLibraryCore.Utilities.ParseTimespan("1234567890m"),
        "Temp-ban length over five digits falls back to the default instead of overflowing");
    Check.Equal(TimeSpan.FromHours(1), SharedLibraryCore.Utilities.ParseTimespan("ban 5m"),
        "Temp-ban length must start the argument");
}
finally
{
    SharedLibraryCore.Utilities.CurrentLocalization = previousLocalization;
}

var directory = Path.Combine(Path.GetTempPath(), "iw4m-regression-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
var factory = new Factory(new DbContextOptionsBuilder().UseSqlite($"Data Source={Path.Combine(directory, "fresh.db")}").Options);
await using (var context = factory.CreateContext())
{
    await context.Database.MigrateAsync();
    await context.Database.OpenConnectionAsync();
    using (var command = context.Database.GetDbConnection().CreateCommand())
    {
        command.CommandText = "SELECT sqlite_version();";
        Check.Equal(true, Version.Parse((string)(await command.ExecuteScalarAsync())!) >= new Version(3, 50, 2),
            "Native SQLite includes the aggregate memory-corruption fix");
        command.CommandText = "SELECT name FROM pragma_index_info('IX_EFACSnapshot_ServerId');";
        Check.Equal("ServerId", (string)(await command.ExecuteScalarAsync())!, "Fresh SQLite snapshot index targets the server column");
    }

    context.Clients.Add(new EFClient
    {
        NetworkId = 998877, GameName = Reference.Game.T5,
        CurrentAlias = new EFAlias { Name = "Regression", Link = new EFAliasLink() }, AliasLink = new EFAliasLink()
    });
    await context.SaveChangesAsync();
}

await FlowChecks.MatchScoreTest();
await ZombieLifecycleChecks.Run();
await StatsChecks.Run(factory);

// Provider snapshots and migration generation can be checked without running a database server.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
using (var postgres = new Data.MigrationContext.PostgresqlDatabaseContext(
           new DbContextOptionsBuilder().UseNpgsql("Host=localhost;Database=regression;Username=regression",
               options => options.SetPostgresVersion(new Version(12, 9))).Options))
using (var mysql = new Data.MigrationContext.MySqlDatabaseContext(
           new DbContextOptionsBuilder().UseMySql("Server=localhost;Database=regression;Uid=regression;",
               new MySqlServerVersion(new Version(8, 0, 35))).Options))
{
    foreach (var provider in new Data.Context.DatabaseContext[] { postgres, mysql })
    {
        Check.Equal(false, provider.Database.HasPendingModelChanges(),
            $"{provider.Database.ProviderName} migration snapshot matches model");
        var migrations = provider.Database.GetMigrations().ToList();
        var script = provider.GetService<IMigrator>().GenerateScript(migrations[^2], migrations[^1]);
        Check.Equal(true, script.Contains("JoinedRound") && script.Contains("RelativeSpeed"), "Provider scoring migration generates SQL");
    }
}

var createOptions = typeof(WebfrontCore.Program).GetMethod("CreateForwardedHeadersOptions", BindingFlags.NonPublic | BindingFlags.Static)!;

async Task ForwardedIp(WebfrontConfiguration configuration, string expected)
{
    var options = (ForwardedHeadersOptions)createOptions.Invoke(null, [configuration])!;
    var context = new DefaultHttpContext();
    context.Connection.RemoteIpAddress = IPAddress.Parse("198.51.100.24");
    context.Request.Headers["X-Forwarded-For"] = "192.0.2.10";
    await new ForwardedHeadersMiddleware(_ => Task.CompletedTask, NullLoggerFactory.Instance, Options.Create(options)).Invoke(context);
    Check.Equal(expected, context.Connection.RemoteIpAddress!.ToString(), "Forwarded-header proxy trust");
}

await ForwardedIp(new WebfrontConfiguration(), "198.51.100.24");
await ForwardedIp(new WebfrontConfiguration { TrustedProxyAddresses = ["198.51.100.24"] }, "192.0.2.10");
await ForwardedIp(new WebfrontConfiguration { TrustedProxyAddresses = ["198.51.100.0/24"] }, "192.0.2.10");
await ForwardedIp(new WebfrontConfiguration { TrustedProxyAddresses = ["not-an-ip", " ", "198.51.100.24 "] }, "192.0.2.10");

var extractionPath = Path.Combine(directory, "extract");
Directory.CreateDirectory(extractionPath);
File.WriteAllText(Path.Combine(extractionPath, "script.gsc"), "OLD");
typeof(PluginImporter).GetMethod("WriteBundleFiles", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null,
    [new Dictionary<string, byte[]> { ["script.gsc"] = "NEW"u8.ToArray() }, extractionPath, true]);
Check.Equal("NEW", File.ReadAllText(Path.Combine(extractionPath, "script.gsc")), "Same-length bundle replacement");
var skippedEntries =
    (List<string>)typeof(PluginImporter).GetMethod("WriteBundleFiles", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null,
    [
        new Dictionary<string, byte[]> { ["../escaped.gsc"] = "X"u8.ToArray(), ["sub/kept.gsc"] = "Y"u8.ToArray() }, extractionPath, true
    ])!;
Check.Equal(false, File.Exists(Path.Combine(directory, "escaped.gsc")), "Bundle entry with '..' cannot escape the extraction folder");
Check.Equal(true, File.Exists(Path.Combine(extractionPath, "sub", "kept.gsc")), "Bundle sub-path entries are still extracted");
Check.Equal(1, skippedEntries.Count, "Escaping bundle entry is reported as skipped");

var thrownByFramework =
    typeof(IW4MAdmin.Application.Program).GetMethod("IsThrownByFrameworkCode", BindingFlags.NonPublic | BindingFlags.Static)!;
bool FrameworkThrown(string stack) => (bool)thrownByFramework.Invoke(null, [stack])!;
Check.Equal(true,
    FrameworkThrown(
        "   at System.ObjectDisposedException.ThrowIf(Boolean condition, Object instance)\n   at Microsoft.Extensions.DependencyInjection.ServiceLookup.ServiceProviderEngineScope.GetService(Type serviceType)\n   at Microsoft.AspNetCore.Components.ComponentFactory.<>c__DisplayClass9_0.<CreatePropertyInjector>g__Initialize|1(IServiceProvider serviceProvider, IComponent component)"),
    "Teardown classifier: disposed-scope property injection is framework-thrown");
Check.Equal(true,
    FrameworkThrown(
        "   at Microsoft.AspNetCore.Components.RenderTree.RenderTreeDiffBuilder.AppendDiffEntriesForFramesWithSameSequence(DiffContext& diffContext, Int32 oldFrameIndex, Int32 newFrameIndex)"),
    "Teardown classifier: null render-tree frame is framework-thrown");
Check.Equal(false,
    FrameworkThrown(
        "   at SomePlugin.Pages.Widget.LoadAsync() in Widget.razor.cs:line 12\n   at Microsoft.AspNetCore.Components.ComponentBase.RunInitAndSetParametersAsync()"),
    "Teardown classifier: a plugin component fault stays an error");

var upgradeFactory = new Factory(new DbContextOptionsBuilder().UseSqlite($"Data Source={Path.Combine(directory, "upgrade.db")}").Options);
await using (var context = upgradeFactory.CreateContext())
{
    var migrator = context.GetService<IMigrator>();
    await migrator.MigrateAsync("20260418155355_DedupeEFMaps");
    string[] samples = ["2026-04-20 12:00:00+00:00", "2026-04-20 12:00:00.1234000+05:30", "2026-04-20 12:00:00.9999000-03:30"];
    foreach (var sample in samples)
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO EFZombieMatches (HighestRound,ClientsCompleted,MatchStartDate,CreatedDateTime) VALUES (3,1,{sample},{sample})");
    await migrator.MigrateAsync();
    var dates = await context.ZombieMatches.OrderBy(m => m.ZombieMatchId).Select(m => m.MatchStartDate).ToListAsync();
    for (var i = 0; i < samples.Length; i++)
        // Stored instants are normalised to UTC (NormalizeZombieTimestampsToUtc) so SQL
        // ordering follows chronology; the instant and its precision must survive.
        Check.Equal(DateTimeOffset.Parse(samples[i]).ToUniversalTime().ToString("O"), dates[i].ToUniversalTime().ToString("O"),
            "SQLite timestamp upgrade preserves the instant and precision");

    // Simulate the already-applied old migration: ISO text remains in INTEGER columns.
    var previousMigration = (await context.Database.GetAppliedMigrationsAsync())
        .TakeWhile(m => !m.EndsWith("PersistZombieRoundScoring")).Last();
    await migrator.MigrateAsync(previousMigration);
    // Reproduce the old constant-expression index without relying on permissive
    // double-quoted string parsing in the patched native SQLite library.
    await context.Database.ExecuteSqlRawAsync(
        "DROP INDEX IX_EFACSnapshot_ServerId; CREATE INDEX IX_EFACSnapshot_ServerId ON EFACSnapshot (('_ServerId' || ''));");
    await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE EFZombieMatches SET MatchStartDate={samples[1]} WHERE ZombieMatchId=1");
    await migrator.MigrateAsync();
    context.ChangeTracker.Clear();
    Check.Equal(DateTimeOffset.Parse(samples[1]).ToUniversalTime().ToString("O"),
        (await context.ZombieMatches.SingleAsync(m => m.ZombieMatchId == 1)).MatchStartDate.ToUniversalTime().ToString("O"),
        "Repair previously applied timestamp migration");
    await context.Database.OpenConnectionAsync();
    using (var command = context.Database.GetDbConnection().CreateCommand())
    {
        command.CommandText = "SELECT name FROM pragma_index_info('IX_EFACSnapshot_ServerId');";
        Check.Equal("ServerId", (string)(await command.ExecuteScalarAsync())!,
            "Upgrade repairs the legacy constant-expression server index");
    }

    await migrator.MigrateAsync("20260418155355_DedupeEFMaps");
    await migrator.MigrateAsync();
    context.ChangeTracker.Clear();
    Check.Equal(DateTimeOffset.Parse(samples[1]).ToUniversalTime().ToString("O"),
        (await context.ZombieMatches.SingleAsync(m => m.ZombieMatchId == 1)).MatchStartDate.ToUniversalTime().ToString("O"),
        "Timestamp downgrade and upgrade round trip");
}

await OpenApiChecks.Run();
var compiler = new CsPluginCompiler(NullLogger<CsPluginCompiler>.Instance);
foreach (var file in Directory.EnumerateFiles(Path.Combine(Check.RepositoryRoot, "Plugins", "ScriptPlugins"), "*.cs"))
{
    var loadContext = new CsPluginLoadContext();
    try
    {
        await compiler.CompileFromFile(file, loadContext);
    }
    finally
    {
        loadContext.Unload();
    }
}

Console.WriteLine("PASS: all shipped C# scripts compile");
Console.WriteLine("Branch regression checks passed.");
