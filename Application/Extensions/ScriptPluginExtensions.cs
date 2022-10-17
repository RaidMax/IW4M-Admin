using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

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
}
