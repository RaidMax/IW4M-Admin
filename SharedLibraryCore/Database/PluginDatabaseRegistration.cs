using System;
using Microsoft.EntityFrameworkCore;

namespace SharedLibraryCore.Database;

/// <summary>
/// A declarative record of a plugin database registered via <c>AddDatabase&lt;TContext&gt;</c>. It carries
/// only data — the context type name, the backing file path, and a non-generic accessor that constructs
/// the plugin's <see cref="DbContext"/>. The host resolves every <see cref="PluginDatabaseRegistration"/>
/// at startup and owns the "how" (apply migrations, set WAL) — before plugins receive the management
/// <c>Load</c> event — so a plugin's schema is ready before its first query. Each database is set up
/// independently so one failure disables only that database.
/// </summary>
public sealed class PluginDatabaseRegistration
{
    public PluginDatabaseRegistration(string contextName, string databasePath,
        Func<DbContext> createContext)
    {
        ContextName = contextName;
        DatabasePath = databasePath;
        CreateContext = createContext;
    }

    /// <summary>The context type name (for diagnostics).</summary>
    public string ContextName { get; }

    /// <summary>Absolute path to the SQLite file backing this context.</summary>
    public string DatabasePath { get; }

    /// <summary>Constructs the plugin's context so the host can apply migrations and set up the database.</summary>
    public Func<DbContext> CreateContext { get; }
}
