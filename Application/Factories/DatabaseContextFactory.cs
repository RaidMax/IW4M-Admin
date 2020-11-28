using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore.Database;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Factories
{
    /// <summary>
    /// implementation of the IDatabaseContextFactory interface
    /// </summary>
    public class DatabaseContextFactory : IDatabaseContextFactory
    {
        private readonly IServiceProvider _serviceProvider;
        
        public DatabaseContextFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }
        
        /// <summary>
        /// creates a new database context
        /// </summary>
        /// <param name="enableTracking">indicates if entity tracking should be enabled</param>
        /// <returns></returns>
        public DatabaseContext CreateContext(bool? enableTracking = true)
        {
            var context = _serviceProvider.GetRequiredService<DatabaseContext>();

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
    }
}
