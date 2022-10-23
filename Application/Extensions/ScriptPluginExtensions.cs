using System.Collections.Generic;
using System.Linq;
using Data.Models.Client.Stats;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;

namespace IW4MAdmin.Application.Extensions;

public static class ScriptPluginExtensions
{
    public static IEnumerable<object> GetClientsBasicData(
        this DbSet<Data.Models.Client.EFClient> set, int[] clientIds)
    {
        return set.Where(client => clientIds.Contains(client.ClientId))
            .Select(client => new
            {
                client.ClientId,
                client.CurrentAlias,
                client.Level,
                client.NetworkId
            }).ToList();
    }

    public static IEnumerable<object> GetClientsStatData(this DbSet<EFClientStatistics> set, int[] clientIds,
        double serverId)
    {
        return set.Where(stat => clientIds.Contains(stat.ClientId) && stat.ServerId == (long)serverId).ToList();
    }

    public static object GetId(this Server server)
    {
        return server.GetIdForServer().GetAwaiter().GetResult();
    }
}
