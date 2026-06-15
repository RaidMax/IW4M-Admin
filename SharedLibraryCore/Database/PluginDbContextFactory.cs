using System;
using Microsoft.EntityFrameworkCore;

namespace SharedLibraryCore.Database;

/// <summary>
/// Runtime <see cref="IDbContextFactory{TContext}"/> for a plugin-owned database. Holds the options
/// privately and is registered by hand (never via <c>AddDbContextFactory</c>) so EF does not register a
/// non-generic <c>DbContextOptions</c> into the shared host container, where it would collide with the
/// host's own <c>DbContext</c>. SQLite by design: a plugin database is self-contained and never shares
/// the host's provider or schema.
/// </summary>
public sealed class PluginDbContextFactory<TContext> : IDbContextFactory<TContext>
    where TContext : DbContext
{
    private readonly DbContextOptions<TContext> _options;

    public PluginDbContextFactory(string connectionString)
    {
        _options = new DbContextOptionsBuilder<TContext>()
            .UseSqlite(connectionString)
            .Options;
    }

    public TContext CreateDbContext() =>
        (TContext)(Activator.CreateInstance(typeof(TContext), _options)
                   ?? throw new InvalidOperationException(
                       $"Could not construct {typeof(TContext).Name}; it must have a public " +
                       $"constructor taking DbContextOptions<{typeof(TContext).Name}>."));
}
