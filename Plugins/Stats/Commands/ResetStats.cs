using IW4MAdmin.Plugins.Stats.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace IW4MAdmin.Plugins.Stats.Commands
{
    public class ResetStats : Command
    {
        private readonly IDatabaseContextFactory _contextFactory;
        
        public ResetStats(CommandConfiguration config, ITranslationLookup translationLookup, 
            IDatabaseContextFactory contextFactory) : base(config, translationLookup)
        {
            Name = "resetstats";
            Description = translationLookup["PLUGINS_STATS_COMMANDS_RESET_DESC"];
            Alias = "rs";
            Permission = EFClient.Permission.User;
            RequiresTarget = false;
            AllowImpersonation = true;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.Origin.ClientNumber >= 0)
            {

                long serverId = Helpers.StatManager.GetIdForServer(E.Owner);

                EFClientStatistics clientStats;
                await using var context = _contextFactory.CreateContext();
                clientStats = await context.Set<EFClientStatistics>()
                    .Where(s => s.ClientId == E.Origin.ClientId)
                    .Where(s => s.ServerId == serverId)
                    .FirstAsync();

                clientStats.Deaths = 0;
                clientStats.Kills = 0;
                clientStats.SPM = 0.0;
                clientStats.Skill = 0.0;
                clientStats.TimePlayed = 0;
                // todo: make this more dynamic
                clientStats.EloRating = 200.0;

                // reset the cached version
                Plugin.Manager.ResetStats(E.Origin);

                await context.SaveChangesAsync();
                E.Origin.Tell(_translationLookup["PLUGINS_STATS_COMMANDS_RESET_SUCCESS"]);
            }

            else
            {
                E.Origin.Tell(_translationLookup["PLUGINS_STATS_COMMANDS_RESET_FAIL"]);
            }
        }
    }
}
