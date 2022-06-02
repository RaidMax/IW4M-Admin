using System.Linq;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models.Client;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using WebfrontCore.QueryHelpers.Models;

namespace WebfrontCore.QueryHelpers;

public class BanInfoResourceQueryHelper : IResourceQueryHelper<BanInfoRequest, BanInfo>
{
    private readonly IDatabaseContextFactory _contextFactory;

    public BanInfoResourceQueryHelper(IDatabaseContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<ResourceQueryHelperResult<BanInfo>> QueryResource(BanInfoRequest query)
    {
        if (query.Count > 30)
        {
            query.Count = 30;
        }
        
        await using var context = _contextFactory.CreateContext(false);

        var matchingClients = await context.Clients.Where(client =>
                EF.Functions.ILike(client.CurrentAlias.SearchableName ?? client.CurrentAlias.Name, $"%{query.ClientName.Trim()}%"))
            .Where(client => client.Level == EFClient.Permission.Banned)
            .OrderByDescending(client => client.LastConnection)
            .Skip(query.Offset)
            .Take(query.Count)
            .Select(client => new
            {
                client.CurrentAlias.Name,
                client.NetworkId,
                client.AliasLinkId,
                client.ClientId
            }).ToListAsync();

        var usedIps = await context.Aliases
            .Where(alias => matchingClients.Select(client => client.AliasLinkId).Contains(alias.LinkId))
            .Where(alias => alias.IPAddress != null)
            .Select(alias => new { alias.IPAddress, alias.LinkId })
            .ToListAsync();

        var usedIpsGrouped = usedIps
            .GroupBy(alias => alias.LinkId)
            .ToDictionary(key => key.Key, value => value.Select(alias => alias.IPAddress).Distinct());

        var searchingNetworkIds = matchingClients.Select(client => client.NetworkId);
        var searchingIps = usedIpsGrouped.SelectMany(group => group.Value);

        var matchedPenalties = await context.PenaltyIdentifiers.Where(identifier =>
                searchingNetworkIds.Contains(identifier.NetworkId) ||
                searchingIps.Contains(identifier.IPv4Address))
            .Select(penalty => new
            {
                penalty.CreatedDateTime,
                PunisherName = penalty.Penalty.Punisher.CurrentAlias.Name,
                Offense = string.IsNullOrEmpty(penalty.Penalty.AutomatedOffense) ? penalty.Penalty.Offense : "Anticheat Detection",
                LinkId = penalty.Penalty.Offender.AliasLinkId,
                penalty.Penalty.PunisherId
            })
            .ToListAsync();
        
        var groupedPenalties = matchedPenalties.GroupBy(penalty => penalty.LinkId)
            .ToDictionary(key => key.Key, value => value.FirstOrDefault());

        var results = matchingClients.Select(client =>
        {
            var matchedPenalty =
                groupedPenalties.ContainsKey(client.AliasLinkId) ? groupedPenalties[client.AliasLinkId] : null;
            return new BanInfo
            {
                DateTime = matchedPenalty?.CreatedDateTime,
                OffenderName = client.Name.StripColors(),
                OffenderId = client.ClientId,
                PunisherName = matchedPenalty?.PunisherName.StripColors(),
                PunisherId = matchedPenalty?.PunisherId,
                Offense = matchedPenalty?.Offense
            };
        }).ToList();

        return new ResourceQueryHelperResult<BanInfo>
        {
            RetrievedResultCount = results.Count,
            TotalResultCount = results.Count,
            Results = results
        };
    }
}
