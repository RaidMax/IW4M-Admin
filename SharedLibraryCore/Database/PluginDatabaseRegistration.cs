using System;
using System.Threading;
using System.Threading.Tasks;

namespace SharedLibraryCore.Database;

/// <summary>
/// A record of a plugin database registered via <c>AddDatabase&lt;TContext&gt;</c>. The host resolves
/// every <see cref="PluginDatabaseRegistration"/> at startup and runs <see cref="MigrateAsync"/> for each
/// — before plugins receive the management <c>Load</c> event — so a plugin's schema is ready before its
/// first query. Each migration is run independently so one failure disables only that database.
/// </summary>
public sealed class PluginDatabaseRegistration
{
    public PluginDatabaseRegistration(string contextName, string databasePath,
        Func<CancellationToken, Task> migrateAsync)
    {
        ContextName = contextName;
        DatabasePath = databasePath;
        MigrateAsync = migrateAsync;
    }

    /// <summary>The context type name (for diagnostics).</summary>
    public string ContextName { get; }

    /// <summary>Absolute path to the SQLite file backing this context.</summary>
    public string DatabasePath { get; }

    /// <summary>Applies pending migrations and sets WAL for this context.</summary>
    public Func<CancellationToken, Task> MigrateAsync { get; }
}
