using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using WebfrontCore.Permissions;
using WebfrontCore.QueryHelpers.Models;
using EFClient = Data.Models.Client.EFClient;

namespace IW4MAdmin.Application.QueryHelpers;

public class ClientResourceQueryHelper : IResourceQueryHelper<ClientResourceRequest, ClientResourceResponse>
{
    public ApplicationConfiguration _appConfig { get; }
    private readonly IDatabaseContextFactory _contextFactory;
    private readonly IGeoLocationService _geoLocationService;

    private class ClientAlias
    {
        public EFClient Client { get; set; }
        public EFAlias Alias { get; set; }
    }

    public ClientResourceQueryHelper(IDatabaseContextFactory contextFactory, IGeoLocationService geoLocationService,
        ApplicationConfiguration appConfig)
    {
        _appConfig = appConfig;
        _contextFactory = contextFactory;
        _geoLocationService = geoLocationService;
    }

    public async Task<ResourceQueryHelperResult<ClientResourceResponse>> QueryResource(ClientResourceRequest query)
    {
        await using var context = _contextFactory.CreateContext(false);
        var iqAliases = context.Aliases.AsQueryable();
        var iqClients = context.Clients.AsQueryable();

        var iqClientAliases = iqClients.Join(iqAliases, client => client.AliasLinkId, alias => alias.LinkId,
            (client, alias) => new ClientAlias { Client = client, Alias = alias });

        return await StartFromClient(query, iqClientAliases, iqClients);
    }

    private async Task<ResourceQueryHelperResult<ClientResourceResponse>> StartFromClient(ClientResourceRequest query,
        IQueryable<ClientAlias> clientAliases, IQueryable<EFClient> iqClients)
    {
        if (!string.IsNullOrWhiteSpace(query.ClientGuid))
        {
            clientAliases = SearchByGuid(query, clientAliases);
        }

        if (query.ClientLevel is not null)
        {
            clientAliases = SearchByLevel(query, clientAliases);
        }

        if (query.ClientConnected is not null)
        {
            clientAliases = SearchByLastConnection(query, clientAliases);
        }

        if (query.GameName is not null)
        {
            clientAliases = SearchByGame(query, clientAliases);
        }

        if (!string.IsNullOrWhiteSpace(query.ClientName))
        {
            clientAliases = SearchByName(query, clientAliases);
        }

        if (!string.IsNullOrWhiteSpace(query.ClientIp))
        {
            clientAliases = SearchByIp(query, clientAliases,
                _appConfig.HasPermission(query.RequesterPermission, WebfrontEntity.ClientIPAddress,
                    WebfrontPermission.Read));
        }

        var iqGroupedClientAliases = clientAliases.GroupBy(a => new { a.Client.ClientId, a.Client.LastConnection });

        iqGroupedClientAliases = query.Direction == SortDirection.Descending
            ? iqGroupedClientAliases.OrderByDescending(clientAlias => clientAlias.Key.LastConnection)
            : iqGroupedClientAliases.OrderBy(clientAlias => clientAlias.Key.LastConnection);

        var clientIds = iqGroupedClientAliases.Select(g => g.Key.ClientId)
            .Skip(query.Offset)
            .Take(query.Count);

        // this pulls in more records than we need, but it's more efficient than ordering grouped entities
        var clientLookups = await clientAliases
            .Where(clientAlias => clientIds.Contains(clientAlias.Client.ClientId))
            .Select(clientAlias => new ClientResourceResponse
            {
                ClientId = clientAlias.Client.ClientId,
                AliasId = clientAlias.Alias.AliasId,
                LinkId = clientAlias.Client.AliasLinkId,
                CurrentClientName = clientAlias.Client.CurrentAlias.Name,
                MatchedClientName = clientAlias.Alias.Name,
                CurrentClientIp = clientAlias.Client.CurrentAlias.IPAddress,
                MatchedClientIp = clientAlias.Alias.IPAddress,
                ClientLevel = clientAlias.Client.Level.ToLocalizedLevelName(),
                ClientLevelValue = clientAlias.Client.Level,
                LastConnection = clientAlias.Client.LastConnection,
                Game = clientAlias.Client.GameName
            })
            .ToListAsync();

        var groupClients = clientLookups.GroupBy(x => x.ClientId);

        var orderedClients = query.Direction == SortDirection.Descending
            ? groupClients.OrderByDescending(SearchByAliasLocal(query.ClientName, query.ClientIp))
            : groupClients.OrderBy(SearchByAliasLocal(query.ClientName, query.ClientIp));

        var clients = orderedClients.Select(client => client.First()).ToList();
        await ProcessAliases(query, clients);

        return new ResourceQueryHelperResult<ClientResourceResponse>
        {
            Results = clients
        };
    }

    private async Task ProcessAliases(ClientResourceRequest query, IEnumerable<ClientResourceResponse> clients)
    {
        await Parallel.ForEachAsync(clients, new ParallelOptions { MaxDegreeOfParallelism = 15 },
            async (client, token) =>
            {
                if (!query.IncludeGeolocationData || client.CurrentClientIp is null)
                {
                    return;
                }

                var geolocationData = await _geoLocationService.Locate(client.CurrentClientIp.ConvertIPtoString());
                client.ClientCountryCode = geolocationData.CountryCode;

                if (!string.IsNullOrWhiteSpace(client.ClientCountryCode))
                {
                    client.ClientCountryDisplayName = geolocationData.Country;
                }
            });
    }

    private static Func<IGrouping<int, ClientResourceResponse>, DateTime> SearchByAliasLocal(string clientName,
        string ipAddress)
    {
        return group =>
        {
            ClientResourceResponse match = null;
            var lowercaseClientName = clientName?.ToLower();

            if (!string.IsNullOrWhiteSpace(lowercaseClientName))
            {
                match = group.ToList().FirstOrDefault(SearchByNameLocal(lowercaseClientName));
            }

            if (match is null && !string.IsNullOrWhiteSpace(ipAddress))
            {
                match = group.ToList().FirstOrDefault(SearchByIpLocal(ipAddress));
            }

            return (match ?? group.First()).LastConnection;
        };
    }

    private static Func<ClientResourceResponse, bool> SearchByNameLocal(string clientName)
    {
        return clientResourceResponse =>
            clientResourceResponse.MatchedClientName.Contains(clientName);
    }

    private static Func<ClientResourceResponse, bool> SearchByIpLocal(string clientIp)
    {
        return clientResourceResponse => clientResourceResponse.MatchedClientIp.ConvertIPtoString().Contains(clientIp);
    }

    private static IQueryable<ClientAlias> SearchByName(ClientResourceRequest query,
        IQueryable<ClientAlias> clientAliases)
    {
        var lowerCaseQueryName = query.ClientName.ToLower();

        clientAliases = clientAliases.Where(query.IsExactClientName
            ? ExactNameMatch(lowerCaseQueryName)
            : LikeNameMatch(lowerCaseQueryName));

        return clientAliases;
    }

    private static Expression<Func<ClientAlias, bool>> LikeNameMatch(string lowerCaseQueryName)
    {
        return clientAlias => EF.Functions.Like(
            clientAlias.Alias.SearchableName,
            $"%{lowerCaseQueryName}%") || EF.Functions.Like(
            clientAlias.Alias.Name.ToLower(),
            $"%{lowerCaseQueryName}%");
    }

    private static Expression<Func<ClientAlias, bool>> ExactNameMatch(string lowerCaseQueryName)
    {
        return clientAlias =>
            lowerCaseQueryName == clientAlias.Alias.Name || lowerCaseQueryName == clientAlias.Alias.SearchableName;
    }

    private static IQueryable<ClientAlias> SearchByIp(ClientResourceRequest query,
        IQueryable<ClientAlias> clientAliases, bool canSearchIP)
    {
        var ipString = query.ClientIp.Trim();
        var ipAddress = ipString.ConvertToIP();

        if (ipAddress != null && ipString.Split('.').Length == 4 && query.IsExactClientIp)
        {
            clientAliases = clientAliases.Where(clientAlias =>
                clientAlias.Alias.IPAddress != null && clientAlias.Alias.IPAddress == ipAddress);
        }
        else if(canSearchIP)
        {
            clientAliases = clientAliases.Where(clientAlias =>
                EF.Functions.Like(clientAlias.Alias.SearchableIPAddress, $"{ipString}%"));
        }

        return clientAliases;
    }

    private static IQueryable<ClientAlias> SearchByGuid(ClientResourceRequest query,
        IQueryable<ClientAlias> clients)
    {
        var guidString = query.ClientGuid.Trim();
        var parsedGuids = new List<long>();
        long guid = 0;

        try
        {
            guid = guidString.ConvertGuidToLong(NumberStyles.HexNumber, false, 0);
        }
        catch
        {
            // ignored
        }

        if (guid != 0)
        {
            parsedGuids.Add(guid);
        }

        try
        {
            guid = guidString.ConvertGuidToLong(NumberStyles.Integer, false, 0);
        }
        catch
        {
            // ignored
        }

        if (guid != 0)
        {
            parsedGuids.Add(guid);
        }

        if (!parsedGuids.Any())
        {
            return clients;
        }

        clients = clients.Where(client => parsedGuids.Contains(client.Client.NetworkId));
        return clients;
    }

    private static IQueryable<ClientAlias> SearchByLevel(ClientResourceRequest query, IQueryable<ClientAlias> clients)
    {
        clients = clients.Where(clientAlias => clientAlias.Client.Level == query.ClientLevel);

        return clients;
    }

    private static IQueryable<ClientAlias> SearchByLastConnection(ClientResourceRequest query,
        IQueryable<ClientAlias> clients)
    {
        clients = clients.Where(clientAlias => clientAlias.Client.LastConnection >= query.ClientConnected);

        return clients;
    }

    private static IQueryable<ClientAlias> SearchByGame(ClientResourceRequest query, IQueryable<ClientAlias> clients)
    {
        clients = clients.Where(clientAlias => clientAlias.Client.GameName == query.GameName);

        return clients;
    }
}
