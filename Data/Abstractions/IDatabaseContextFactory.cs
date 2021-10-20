using Data.Context;

namespace Data.Abstractions
{
    /// <summary>
    /// describes the capabilities of the database context factory
    /// </summary>
    public interface IDatabaseContextFactory
    {
        /// <summary>
        /// create or retrieves an existing database context instance
        /// </summary>
        /// <param name="enableTracking">indicated if entity tracking should be enabled</param>
        /// <returns>database context instance</returns>
        DatabaseContext CreateContext(bool? enableTracking = true);
    }
}
