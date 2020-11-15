using System;
using System.Linq;
using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore.Database;

namespace IW4MAdmin.Application.Migration
{
    public static class DatabaseHousekeeping
    {
        private static DateTime _cutoffDate = DateTime.UtcNow.AddMonths(-6);
        
        public static void RemoveOldRatings(DatabaseContext context)
        {
            var dbSet = context.Set<EFRating>();
            var itemsToDelete = dbSet.Where(rating => rating.When <= _cutoffDate);
            dbSet.RemoveRange(itemsToDelete);
            context.SaveChanges();
        }
    }
}