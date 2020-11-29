using System;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.MigrationContext;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Factories
{
    /// <summary>
    /// implementation of the IDatabaseContextFactory interface
    /// </summary>
    public class DatabaseContextFactory : IDatabaseContextFactory
    {
        private readonly DbContextOptions _contextOptions;
        private readonly string _activeProvider;

        public DatabaseContextFactory(ApplicationConfiguration appConfig, DbContextOptions contextOptions)
        {
            _contextOptions = contextOptions;
            _activeProvider = appConfig.DatabaseProvider?.ToLower();
        }

        /// <summary>
        /// creates a new database context
        /// </summary>
        /// <param name="enableTracking">indicates if entity tracking should be enabled</param>
        /// <returns></returns>
        public DatabaseContext CreateContext(bool? enableTracking = true)
        {
            var context = BuildContext();

            enableTracking ??= true;

            if (enableTracking.Value)
            {
                context.ChangeTracker.AutoDetectChangesEnabled = true;
                context.ChangeTracker.LazyLoadingEnabled = true;
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
            }
            else
            {
                context.ChangeTracker.AutoDetectChangesEnabled = false;
                context.ChangeTracker.LazyLoadingEnabled = false;
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            }

            return context;
        }

        private DatabaseContext BuildContext()
        {
            return _activeProvider switch
            {
                "sqlite" => new SqliteDatabaseContext(_contextOptions),
                "mysql" => new MySqlDatabaseContext(_contextOptions),
                "postgresql" => new PostgresqlDatabaseContext(_contextOptions),
                _ => throw new ArgumentException($"No context found for {_activeProvider}")
            };
        }
    }
}