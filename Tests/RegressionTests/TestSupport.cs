using Data.Abstractions;
using Data.Context;
using Data.MigrationContext;
using Microsoft.EntityFrameworkCore;

internal class Factory(DbContextOptions options) : IDatabaseContextFactory
{
    public DatabaseContext CreateContext(bool? enableTracking = true)
    {
        var context = new SqliteDatabaseContext(options);
        if (enableTracking == false)
        {
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            context.ChangeTracker.AutoDetectChangesEnabled = false;
        }
        return context;
    }
}

internal static class Check
{
    internal static IW4MAdmin.IW4MServer Server(Data.Models.Server.EFServer metadata,
        Data.Models.Reference.Game game = Data.Models.Reference.Game.T5, string gametype = "dm")
    {
        var server = (IW4MAdmin.IW4MServer)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(IW4MAdmin.IW4MServer));
        var type = typeof(SharedLibraryCore.Server);
        type.GetProperty("IP")!.SetValue(server, "127.0.0.1");
        type.GetProperty("Port")!.SetValue(server, metadata.Port);
        type.GetProperty("GameName")!.SetValue(server, game);
        type.GetProperty("Gametype")!.SetValue(server, gametype);
        type.GetProperty("Clients")!.SetValue(server, new List<SharedLibraryCore.Database.Models.EFClient>());
        type.GetProperty("PerformanceCode")!.SetValue(server, metadata.PerformanceBucket?.Code);
        type.GetField("<ServerConfig>k__BackingField", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(server, new SharedLibraryCore.Configuration.ServerConfiguration());
        typeof(IW4MAdmin.IW4MServer).GetField("_cachedDatabaseServer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .SetValue(server, metadata);
        return server;
    }

    public static void Equal<T>(T expected, T actual, string name)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
        Console.WriteLine($"PASS: {name}");
    }
    public static string RepositoryRoot
    {
        get
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "IW4MAdmin.slnx"))) return dir.FullName;
            throw new InvalidOperationException("Run from the repository's test output directory");
        }
    }
}
