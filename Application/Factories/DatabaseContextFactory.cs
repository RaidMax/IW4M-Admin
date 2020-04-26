using SharedLibraryCore.Database;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Factories
{
    /// <summary>
    /// implementation of the IDatabaseContextFactory interface
    /// </summary>
    public class DatabaseContextFactory : IDatabaseContextFactory
    {
        /// <summary>
        /// creates a new database context
        /// </summary>
        /// <param name="enableTracking">indicates if entity tracking should be enabled</param>
        /// <returns></returns>
        public DatabaseContext CreateContext(bool? enableTracking = true)
        {
            return enableTracking.HasValue ? new DatabaseContext(disableTracking: !enableTracking.Value) : new DatabaseContext();
        }
    }
}
