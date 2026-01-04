using System.Globalization;
using Data.Abstractions;
using Data.Models;
using Data.Models.Client;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Core.QueryHelpers.Models;

namespace WebfrontCore.Core.QueryHelpers;

public class BanInfoResourceQueryHelper(IDatabaseContextFactory contextFactory)
    : IResourceQueryHelper<BanInfoRequest, BanInfo>
{
    public async Task<ResourceQueryHelperResult<BanInfo>> QueryResource(BanInfoRequest query)
    {
        if (string.IsNullOrEmpty(query.ClientName) && string.IsNullOrEmpty(query.ClientGuid) &&
            query.ClientId is null && string.IsNullOrEmpty(query.ClientIP))
        {
            return new ResourceQueryHelperResult<BanInfo>
            {
                Results = []
            };
        }

        await using var context = contextFactory.CreateContext(false);

        var iqMatchingClients = context.Clients.Where(client => client.Level == EFClient.Permission.Banned);
        iqMatchingClients = SetupSearchArgs(query, iqMatchingClients);

        var matchingClients = await iqMatchingClients
            .OrderByDescending(client => client.LastConnection)
            .Skip(query.Offset)
            .Take(query.Count)
            .Select(client => new
            {
                client.CurrentAlias.Name,
                client.NetworkId,
                client.AliasLinkId,
                client.ClientId,
                client.CurrentAlias.IPAddress,
                client.GameName
            }).ToListAsync();

        var results = new List<BanInfo>();
        var matchedClientIds = new List<int?>();
        var lateDateTime = DateTime.Now.AddYears(100);
        var totalResultCount = 0;
        // would prefer not to loop this, but unfortunately due to the data design 
        // we can't properly group on ip and alias link
        foreach (var matchingClient in matchingClients)
        {
            var usedIps = await context.Aliases
                .Where(alias => matchingClient.AliasLinkId == alias.LinkId)
                .Where(alias => alias.IPAddress != null)
                .Select(alias => new { alias.IPAddress, alias.LinkId })
                .ToListAsync();

            var searchingNetworkId = matchingClient.NetworkId;
            var searchingIps = usedIps.Select(ip => ip.IPAddress).Distinct();

            var matchedPenalties = await context.PenaltyIdentifiers.Where(identifier =>
                    identifier.NetworkId == searchingNetworkId ||
                    searchingIps.Contains(identifier.IPv4Address))
                .Where(identifier => identifier.Penalty.Expires == null || identifier.Penalty.Expires > lateDateTime)
                .Select(penalty => new
                {
                    penalty.CreatedDateTime,
                    PunisherName = penalty.Penalty.Punisher.CurrentAlias.Name,
                    OffenderName = penalty.Penalty.Offender.CurrentAlias.Name,
                    Offense = string.IsNullOrEmpty(penalty.Penalty.AutomatedOffense)
                        ? penalty.Penalty.Offense
                        : Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_BAN_INFO_AC_DETECTION"],
                    LinkId = penalty.Penalty.Offender.AliasLinkId,
                    penalty.Penalty.OffenderId,
                    penalty.Penalty.PunisherId,
                    penalty.IPv4Address,
                    penalty.Penalty.Offender.NetworkId
                })
                .ToListAsync();

            if (matchedPenalties.Count == 0)
            {
                var linkIds = (await context.Aliases
                    .Where(alias => alias.IPAddress != null && searchingIps.Contains(alias.IPAddress))
                    .Select(alias => alias.LinkId)
                    .ToListAsync()).Distinct();

                matchedPenalties = await context.Penalties.Where(penalty => penalty.Type == EFPenalty.PenaltyType.Ban)
                    .Where(penalty => penalty.Expires == null || penalty.Expires > lateDateTime)
                    .Where(penalty => penalty.LinkId != null && linkIds.Contains(penalty.LinkId.Value))
                    .OrderByDescending(penalty => penalty.When)
                    .Select(penalty => new
                    {
                        CreatedDateTime = penalty.When,
                        PunisherName = penalty.Punisher.CurrentAlias.Name,
                        OffenderName = penalty.Offender.CurrentAlias.Name,
                        Offense = string.IsNullOrEmpty(penalty.AutomatedOffense)
                            ? penalty.Offense
                            : Utilities.CurrentLocalization.LocalizationIndex["WEBFRONT_BAN_INFO_AC_DETECTION"],
                        LinkId = penalty.Offender.AliasLinkId,
                        penalty.OffenderId,
                        penalty.PunisherId,
                        IPv4Address = penalty.Offender.CurrentAlias.IPAddress,
                        penalty.Offender.NetworkId
                    }).ToListAsync();
            }

            var allPenalties = matchedPenalties.Select(penalty => new PenaltyInfo
            {
                DateTime = penalty.CreatedDateTime,
                Offense = penalty.Offense,
                PunisherInfo = new RelatedClientInfo
                {
                    ClientName = penalty.PunisherName.StripColors(),
                    ClientId = penalty.PunisherId,
                },
                OffenderInfo = new RelatedClientInfo
                {
                    ClientName = penalty.OffenderName.StripColors(),
                    ClientId = penalty.OffenderId,
                    IPAddress = penalty.IPv4Address,
                    NetworkId = penalty.NetworkId
                }
            }).ToList();

            totalResultCount += allPenalties.Count;

            if (matchedClientIds.Contains(matchingClient.ClientId))
            {
                continue;
            }

            matchedClientIds.Add(matchingClient.ClientId);
            var relatedEntities =
                allPenalties.Where(penalty => penalty.OffenderInfo.ClientId != matchingClient.ClientId)
                    .ToList();

            matchedClientIds.AddRange(relatedEntities.Select(client => client.OffenderInfo.ClientId));

            results.Add(new BanInfo
            {
                ClientName = matchingClient.Name.StripColors(),
                ClientId = matchingClient.ClientId,
                NetworkId = matchingClient.NetworkId,
                IPAddress = matchingClient.IPAddress,
                Game = matchingClient.GameName,

                AssociatedPenalties = relatedEntities,
                AttachedPenalty = allPenalties.FirstOrDefault(penalty =>
                    penalty.OffenderInfo.ClientId == matchingClient.ClientId)
            });
        }

        return new ResourceQueryHelperResult<BanInfo>
        {
            RetrievedResultCount = results.Count,
            // this will technically be incorrect if the total # of matching players > pagination count
            // however this should not be the case for the majority of searches
            TotalResultCount = totalResultCount,
            Results = results
        };
    }

    private static IQueryable<EFClient> SetupSearchArgs(BanInfoRequest query, IQueryable<EFClient> source)
    {
        if (!string.IsNullOrEmpty(query.ClientName))
        {
            var nameToSearch = query.ClientName.Trim().ToLower();
            source = source.Where(client =>
                EF.Functions.Like(client.CurrentAlias.SearchableName ?? client.CurrentAlias.Name.ToLower(),
                    $"%{nameToSearch}%"));
        }

        if (!string.IsNullOrEmpty(query.ClientGuid))
        {
            long? parsedGuid = null;
            if (!long.TryParse(query.ClientGuid, NumberStyles.HexNumber, null, out var guid))
            {
                if (!long.TryParse(query.ClientGuid, out var guid2))
                {
                }
                else
                {
                    parsedGuid = guid2;
                }
            }
            else
            {
                parsedGuid = guid;
            }

            if (parsedGuid is not null)
            {
                source = source.Where(client => client.NetworkId == parsedGuid);
            }
        }

        if (query.ClientId is not null)
        {
            source = source.Where(client => client.ClientId == query.ClientId);
        }

        if (string.IsNullOrEmpty(query.ClientIP))
        {
            return source;
        }

        var parsedIp = query.ClientIP.ConvertToIP();
        if (parsedIp is not null)
        {
            source = source.Where(client => client.CurrentAlias.IPAddress == parsedIp);
        }
        else
        {
            query.ClientIP = null;
        }

        return source;
    }
}
