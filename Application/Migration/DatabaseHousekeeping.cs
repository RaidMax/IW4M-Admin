﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore.Database;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Migration
{
    public static class DatabaseHousekeeping
    {
        private static readonly DateTime CutoffDate = DateTime.UtcNow.AddMonths(-6);
        
        public static async Task RemoveOldRatings(IDatabaseContextFactory contextFactory, CancellationToken token)
        {
            await using var context = contextFactory.CreateContext();
            var dbSet = context.Set<EFRating>();
            var itemsToDelete = dbSet.Where(rating => rating.When <= CutoffDate);
            dbSet.RemoveRange(itemsToDelete);
            await context.SaveChangesAsync(token);
        }
    }
}