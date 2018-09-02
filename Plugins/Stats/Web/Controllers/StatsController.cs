using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using SharedLibraryCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WebfrontCore.Controllers;

namespace IW4MAdmin.Plugins.Stats.Web.Controllers
{
    public class StatsController : BaseController
    {
        [HttpGet]
        public async Task<IActionResult> TopPlayersAsync()
        {
            ViewBag.Title = Utilities.CurrentLocalization.LocalizationIndex.Set["WEBFRONT_STATS_INDEX_TITLE"];
            ViewBag.Description = Utilities.CurrentLocalization.LocalizationIndex.Set["WEBFRONT_STATS_INDEX_DESC"];

            return View("Index", await Plugin.Manager.GetTopStats(0, 10));
        }

        [HttpGet]
        public async Task<IActionResult> GetTopPlayersAsync(int count, int offset)
        {
            return View("_List", await Plugin.Manager.GetTopStats(offset, count));
        }

        [HttpGet]
        public async Task<IActionResult> GetMessageAsync(int serverId, DateTime when)
        {
            var whenUpper = when.AddMinutes(5);
            var whenLower = when.AddMinutes(-5);

            using (var ctx = new SharedLibraryCore.Database.DatabaseContext())
            {
                var iqMessages = from message in ctx.Set<Models.EFClientMessage>()
                                 where message.ServerId == serverId
                                 where message.TimeSent >= whenLower
                                 where message.TimeSent <= whenUpper
                                 select new SharedLibraryCore.Dtos.ChatInfo()
                                 {
                                     Message = message.Message,
                                     Name = message.Client.CurrentAlias.Name,
                                     Time = message.TimeSent
                                 };

                var messages = await iqMessages.ToListAsync();
                string sql = iqMessages.ToSql();

                return View("_MessageContext", messages);
            }
        }

        [HttpGet]
      //  [Authorize]
        public async Task<IActionResult> GetAutomatedPenaltyInfoAsync(int clientId)
        {
            using (var ctx = new SharedLibraryCore.Database.DatabaseContext())
            {
                var penaltyInfo = await ctx.Set<Models.EFACSnapshot>()
                    .Where(s => s.ClientId == clientId)
                    .Include(s => s.LastStrainAngle)
                    .Include(s => s.HitOrigin)
                    .Include(s => s.HitDestination)
                    .Include(s => s.CurrentViewAngle)
                    .Include(s => s.PredictedViewAngles)
                    .OrderBy(s => new { s.When, s.Hits })
                    .ToListAsync();

                if (penaltyInfo != null)
                {
                    return View("_PenaltyInfo", penaltyInfo);
                }

                return NotFound();
            }
        }
    }

    public static class IQueryableExtensions
    {
        private static readonly TypeInfo QueryCompilerTypeInfo = typeof(QueryCompiler).GetTypeInfo();

        private static readonly FieldInfo QueryCompilerField = typeof(EntityQueryProvider).GetTypeInfo().DeclaredFields.First(x => x.Name == "_queryCompiler");

        private static readonly FieldInfo QueryModelGeneratorField = QueryCompilerTypeInfo.DeclaredFields.First(x => x.Name == "_queryModelGenerator");

        private static readonly FieldInfo DataBaseField = QueryCompilerTypeInfo.DeclaredFields.Single(x => x.Name == "_database");

        private static readonly PropertyInfo DatabaseDependenciesField = typeof(Database).GetTypeInfo().DeclaredProperties.Single(x => x.Name == "Dependencies");

        public static string ToSql<TEntity>(this IQueryable<TEntity> query) where TEntity : class
        {
            var queryCompiler = (QueryCompiler)QueryCompilerField.GetValue(query.Provider);
            var modelGenerator = (QueryModelGenerator)QueryModelGeneratorField.GetValue(queryCompiler);
            var queryModel = modelGenerator.ParseQuery(query.Expression);
            var database = (IDatabase)DataBaseField.GetValue(queryCompiler);
            var databaseDependencies = (DatabaseDependencies)DatabaseDependenciesField.GetValue(database);
            var queryCompilationContext = databaseDependencies.QueryCompilationContextFactory.Create(false);
            var modelVisitor = (RelationalQueryModelVisitor)queryCompilationContext.CreateQueryModelVisitor();
            modelVisitor.CreateQueryExecutor<TEntity>(queryModel);
            var sql = modelVisitor.Queries.First().ToString();

            return sql;
        }
    }
}
