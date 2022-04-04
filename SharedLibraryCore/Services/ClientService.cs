using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Data.Abstractions;
using Data.Context;
using Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using static Data.Models.Client.EFClient;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SharedLibraryCore.Services
{
    public class ClientService : IEntityService<EFClient>, IResourceQueryHelper<FindClientRequest, FindClientResult>
    {
        private static readonly Func<DatabaseContext, long, Task<EFClient>> _getUniqueQuery =
            EF.CompileAsyncQuery((DatabaseContext context, long networkId) =>
                context.Clients
                    .Select(_client => new EFClient
                    {
                        ClientId = _client.ClientId,
                        AliasLinkId = _client.AliasLinkId,
                        Level = _client.Level,
                        Connections = _client.Connections,
                        FirstConnection = _client.FirstConnection,
                        LastConnection = _client.LastConnection,
                        Masked = _client.Masked,
                        NetworkId = _client.NetworkId,
                        TotalConnectionTime = _client.TotalConnectionTime,
                        AliasLink = _client.AliasLink,
                        Password = _client.Password,
                        PasswordSalt = _client.PasswordSalt
                    })
                    .FirstOrDefault(c => c.NetworkId == networkId)
            );

        private readonly ApplicationConfiguration _appConfig;
        private readonly IDatabaseContextFactory _contextFactory;
        private readonly ILogger _logger;

        public ClientService(ILogger<ClientService> logger, IDatabaseContextFactory databaseContextFactory,
            ApplicationConfiguration appConfig)
        {
            _contextFactory = databaseContextFactory;
            _logger = logger;
            _appConfig = appConfig;
        }

        public async Task<EFClient> Create(EFClient entity)
        {
            entity.Name = entity.Name.CapClientName(EFAlias.MAX_NAME_LENGTH);

            if (!_appConfig.EnableImplicitAccountLinking)
            {
                return await HandleNewCreate(entity);
            }

            await using var context = _contextFactory.CreateContext();
            using (LogContext.PushProperty("Server", entity?.CurrentServer?.ToString()))
            {
                int? linkId = null;
                int? aliasId = null;

                if (entity.IPAddress != null)
                {
                    var existingAliases = await context.Aliases
                        .Select(_alias => new { _alias.AliasId, _alias.LinkId, _alias.IPAddress, _alias.Name })
                        .Where(_alias => _alias.IPAddress == entity.IPAddress)
                        .ToListAsync();

                    if (existingAliases.Count > 0)
                    {
                        linkId = existingAliases.OrderBy(_alias => _alias.LinkId).First().LinkId;

                        _logger.LogDebug("[create] client with new GUID {entity} has existing link {linkId}",
                            entity.ToString(), linkId);

                        var existingExactAlias = existingAliases.FirstOrDefault(_alias => _alias.Name == entity.Name);

                        if (existingExactAlias != null)
                        {
                            _logger.LogDebug("[create] client with new GUID {entity} has existing alias {aliasId}",
                                entity.ToString(), existingExactAlias.AliasId);
                            aliasId = existingExactAlias.AliasId;
                        }
                    }
                }

                var client = new EFClient
                {
                    Level = Permission.User,
                    FirstConnection = DateTime.UtcNow,
                    LastConnection = DateTime.UtcNow,
                    NetworkId = entity.NetworkId
                };

                _logger.LogDebug("[create] adding {entity} to context", entity.ToString());


                // they're just using a new GUID
                if (aliasId.HasValue)
                {
                    _logger.LogDebug("[create] setting {entity}'s alias id and linkid to ({aliasId}, {linkId})",
                        entity.ToString(), aliasId, linkId);
                    client.CurrentAliasId = aliasId.Value;
                    client.AliasLinkId = linkId.Value;
                }

                // link was found but they don't have an exact alias
                else if (!aliasId.HasValue && linkId.HasValue)
                {
                    _logger.LogDebug("[create] setting {entity}'s linkid to {linkId}, but creating new alias",
                        entity.ToString(), linkId);
                    client.AliasLinkId = linkId.Value;
                    client.CurrentAlias = new EFAlias
                    {
                        Name = entity.Name,
                        SearchableName = entity.Name.StripColors().ToLower(),
                        DateAdded = DateTime.UtcNow,
                        IPAddress = entity.IPAddress,
                        LinkId = linkId.Value
                    };
                }

                // brand new players (supposedly)
                else
                {
                    _logger.LogDebug("[create] creating new Link and Alias for {entity}", entity.ToString());
                    var link = new EFAliasLink();
                    var alias = new EFAlias
                    {
                        Name = entity.Name,
                        SearchableName = entity.Name.StripColors().ToLower(),
                        DateAdded = DateTime.UtcNow,
                        IPAddress = entity.IPAddress,
                        Link = link
                    };

                    client.AliasLink = link;
                    client.CurrentAlias = alias;
                }

                context.Clients.Add(client);
                await context.SaveChangesAsync();

                return client;
            }
        }

        public Task<EFClient> Delete(EFClient entity)
        {
            throw new NotImplementedException();
        }

        public Task<IList<EFClient>> Find(Func<EFClient, bool> e)
        {
            throw new NotImplementedException();
        }

        public async Task<EFClient> Get(int entityId)
        {
            await using var context = _contextFactory.CreateContext(false);

            var client = await context.Clients
                .Select(_client => new EFClient
                {
                    ClientId = _client.ClientId,
                    AliasLinkId = _client.AliasLinkId,
                    Level = _client.Level,
                    Connections = _client.Connections,
                    FirstConnection = _client.FirstConnection,
                    LastConnection = _client.LastConnection,
                    Masked = _client.Masked,
                    NetworkId = _client.NetworkId,
                    CurrentAlias = new EFAlias
                    {
                        Name = _client.CurrentAlias.Name,
                        IPAddress = _client.CurrentAlias.IPAddress
                    },
                    TotalConnectionTime = _client.TotalConnectionTime,
                    AliasLink = new EFAliasLink
                    {
                        AliasLinkId = _client.AliasLinkId,
                        Children = _client.AliasLink.Children
                    },
                    LinkedAccounts = new Dictionary<int, long>()
                    {
                        {_client.ClientId, _client.NetworkId}   
                    }
                })
                .FirstOrDefaultAsync(_client => _client.ClientId == entityId);

            if (client == null)
            {
                return null;
            }

            if (!_appConfig.EnableImplicitAccountLinking)
            {
                return client;
            }

            var foundClient = new
            {
                Client = client,
                LinkedAccounts = await context.Clients.Where(_client => _client.AliasLinkId == client.AliasLinkId)
                    .Select(_linkedClient => new
                    {
                        _linkedClient.ClientId,
                        _linkedClient.NetworkId
                    })
                    .ToListAsync()
            };

            foundClient.Client.LinkedAccounts = new Dictionary<int, long>();
            // todo: find out the best way to do this
            // I'm doing this here because I don't know the best way to have multiple awaits in the query
            foreach (var linked in foundClient.LinkedAccounts)
                foundClient.Client.LinkedAccounts.Add(linked.ClientId, linked.NetworkId);

            return foundClient.Client;
        }

        public virtual async Task<EFClient> GetUnique(long entityAttribute)
        {
            await using var context = _contextFactory.CreateContext(false);
            return await _getUniqueQuery(context, entityAttribute);
        }

        public async Task<EFClient> Update(EFClient temporalClient)
        {
            if (temporalClient.ClientId < 1)
            {
                _logger.LogDebug(
                    "[update] {client} needs to be updated but they do not have a valid client id, ignoring..",
                    temporalClient.ToString());
                // note: we never do anything with the result of this so we can safely return null
                return null;
            }

            await using var context = _contextFactory.CreateContext();

            // grab the context version of the entity
            var entity = context.Clients
                .First(client => client.ClientId == temporalClient.ClientId);

            if (temporalClient.LastConnection > entity.LastConnection)
            {
                entity.LastConnection = temporalClient.LastConnection;
            }

            if (temporalClient.Connections > entity.Connections)
            {
                entity.Connections = temporalClient.Connections;
            }

            entity.Masked = temporalClient.Masked;

            if (temporalClient.TotalConnectionTime > entity.TotalConnectionTime)
            {
                entity.TotalConnectionTime = temporalClient.TotalConnectionTime;
            }

            if (temporalClient.Password != null)
            {
                entity.Password = temporalClient.Password;
            }

            if (temporalClient.PasswordSalt != null)
            {
                entity.PasswordSalt = temporalClient.PasswordSalt;
            }

            // update in database
            await context.SaveChangesAsync();
            return entity.ToPartialClient();
        }

        /// <summary>
        ///     find clients matching the given query
        /// </summary>
        /// <param name="query">query filters</param>
        /// <returns></returns>
        public async Task<ResourceQueryHelperResult<FindClientResult>> QueryResource(FindClientRequest query)
        {
            var result = new ResourceQueryHelperResult<FindClientResult>();
            await using var context = _contextFactory.CreateContext(false);

            IQueryable<Data.Models.Client.EFClient> iqClients = null;

            if (!string.IsNullOrEmpty(query.Xuid))
            {
                var networkId = query.Xuid.ConvertGuidToLong(NumberStyles.HexNumber);
                iqClients = context.Clients.Where(_client => _client.NetworkId == networkId);
            }

            else if (!string.IsNullOrEmpty(query.Name))
            {
                iqClients = context.Clients
                    .Where(_client =>
                        EF.Functions.Like(_client.CurrentAlias.Name.ToLower(), $"%{query.Name.ToLower()}%"));
            }

            if (query.Direction == SortDirection.Ascending)
            {
                iqClients = iqClients.OrderBy(_client => _client.LastConnection);
            }

            else
            {
                iqClients = iqClients.OrderByDescending(_client => _client.LastConnection);
            }

            var queryResults = await iqClients
                .Select(_client => new FindClientResult
                {
                    ClientId = _client.ClientId,
                    Xuid = _client.NetworkId.ToString("X"),
                    Name = _client.CurrentAlias.Name
                })
                .Skip(query.Offset)
                .Take(query.Count)
                .ToListAsync();

            result.TotalResultCount = await iqClients.CountAsync();
            result.Results = queryResults;
            result.RetrievedResultCount = queryResults.Count;

            return result;
        }

        private async Task<EFClient> HandleNewCreate(EFClient entity)
        {
            await using var context = _contextFactory.CreateContext();
            using (LogContext.PushProperty("Server", entity.CurrentServer?.ToString()))
            {
                var existingAlias = await context.Aliases
                    .Select(alias => new { alias.AliasId, alias.LinkId, alias.IPAddress, alias.Name })
                    .Where(alias => alias.IPAddress != null && alias.IPAddress == entity.IPAddress &&
                                    alias.Name == entity.Name)
                    .FirstOrDefaultAsync();

                var client = new EFClient
                {
                    Level = Permission.User,
                    FirstConnection = DateTime.UtcNow,
                    LastConnection = DateTime.UtcNow,
                    NetworkId = entity.NetworkId
                };

                if (existingAlias == null)
                {
                    _logger.LogDebug("[{Method}] creating new Link and Alias for {Entity}", nameof(HandleNewCreate),
                        entity.ToString());
                    var link = new EFAliasLink();
                    var alias = new EFAlias
                    {
                        Name = entity.Name,
                        SearchableName = entity.Name.StripColors().ToLower(),
                        DateAdded = DateTime.UtcNow,
                        IPAddress = entity.IPAddress,
                        Link = link
                    };
                    client.CurrentAlias = alias;
                    client.AliasLink = link;
                }

                else
                {
                    _logger.LogDebug(
                        "[{Method}] associating new GUID {Guid} with new exact alias match with linkId {LinkId} for {Entity}",
                        nameof(HandleNewCreate), entity.GuidString, existingAlias.LinkId, entity.ToString());

                    var alias = new EFAlias
                    {
                        Name = existingAlias.Name,
                        SearchableName = entity.Name.StripColors().ToLower(),
                        DateAdded = DateTime.UtcNow,
                        IPAddress = entity.IPAddress,
                        LinkId = existingAlias.LinkId
                    };
                    client.CurrentAlias = alias;
                    client.AliasLinkId = existingAlias.LinkId;
                }

                context.Clients.Add(client);
                await context.SaveChangesAsync();
                return client;
            }
        }

        private async Task UpdateAlias(string originalName, int? ip, Data.Models.Client.EFClient entity,
            DatabaseContext context)
        {
            {
                var name = originalName.CapClientName(EFAlias.MAX_NAME_LENGTH);

                // entity is the tracked db context item
                // get all aliases by IP address and LinkId
                var iqAliases = context.Aliases
                    .Include(a => a.Link)
                    // we only want alias that have the same IP address or share a link
                    .Where(_alias => _alias.IPAddress == ip || _alias.LinkId == entity.AliasLinkId);

                var aliases = await iqAliases.ToListAsync();
                var currentIPs = aliases.Where(_a2 => _a2.IPAddress != null).Select(_a2 => _a2.IPAddress).Distinct();
                var floatingIPAliases = await context.Aliases.Where(_alias => currentIPs.Contains(_alias.IPAddress))
                    .ToListAsync();
                aliases.AddRange(floatingIPAliases);

                // see if they have a matching IP + Name but new NetworkId
                var existingExactAlias = aliases.OrderBy(_alias => _alias.LinkId)
                    .FirstOrDefault(a => a.Name == name && a.IPAddress == ip);
                var hasExactAliasMatch = existingExactAlias != null;

                // if existing alias matches link them
                var newAliasLink = existingExactAlias?.Link;
                // if no exact matches find the first IP or LinkId that matches
                newAliasLink = newAliasLink ?? aliases.OrderBy(_alias => _alias.LinkId).FirstOrDefault()?.Link;
                // if no matches are found, use our current one ( it will become permanent )
                newAliasLink = newAliasLink ?? entity.AliasLink;

                var hasExistingAlias = aliases.Count > 0;
                var isAliasLinkUpdated = newAliasLink.AliasLinkId != entity.AliasLink.AliasLinkId;

                await context.SaveChangesAsync();
                var distinctLinkCount = aliases.Select(_alias => _alias.LinkId).Distinct().Count();
                // this happens when the link we found is different than the one we create before adding an IP
                if (isAliasLinkUpdated || distinctLinkCount > 1)
                {
                    _logger.LogDebug(
                        "[updatealias] found a link for {entity} so we are updating link from {oldAliasLinkId} to {newAliasLinkId}",
                        entity.ToString(), entity.AliasLink.AliasLinkId, newAliasLink.AliasLinkId);

                    var completeAliasLinkIds = aliases.Select(_item => _item.LinkId)
                        .Append(entity.AliasLinkId)
                        .Distinct()
                        .ToList();

                    _logger.LogDebug("[updatealias] updating aliasLinks {links} for IP {ip} to {linkId}",
                        string.Join(',', completeAliasLinkIds), ip, newAliasLink.AliasLinkId);

                    // update all the clients that have the old alias link
                    await context.Clients
                        .Where(_client => completeAliasLinkIds.Contains(_client.AliasLinkId))
                        .ForEachAsync(_client => _client.AliasLinkId = newAliasLink.AliasLinkId);

                    // we also need to update all the penalties or they get deleted
                    // scenario
                    // link1 joins with ip1
                    // link2 joins with ip2,
                    // link2 receives penalty
                    // link2 joins with ip1
                    // pre existing link for link2 detected
                    // link2 is deleted
                    // link2 penalties are orphaned
                    await context.Penalties
                        .Where(_penalty => completeAliasLinkIds.Contains(_penalty.LinkId ?? -1))
                        .ForEachAsync(_penalty => _penalty.LinkId = newAliasLink.AliasLinkId);

                    entity.AliasLink = newAliasLink;
                    entity.AliasLinkId = newAliasLink.AliasLinkId;

                    // update all previous aliases
                    await context.Aliases
                        .Where(_alias => completeAliasLinkIds.Contains(_alias.LinkId))
                        .ForEachAsync(_alias => _alias.LinkId = newAliasLink.AliasLinkId);

                    await context.SaveChangesAsync();
                    // we want to delete the now inactive alias
                    if (newAliasLink.AliasLinkId != entity.AliasLinkId)
                    {
                        context.AliasLinks.Remove(entity.AliasLink);
                        await context.SaveChangesAsync();
                    }
                }

                // the existing alias matches ip and name, so we can just ignore the temporary one
                if (hasExactAliasMatch)
                {
                    _logger.LogDebug("[updatealias] {entity} has exact alias match", entity.ToString());

                    var oldAlias = entity.CurrentAlias;
                    entity.CurrentAliasId = existingExactAlias.AliasId;
                    entity.CurrentAlias = existingExactAlias;
                    await context.SaveChangesAsync();

                    // the alias is the same so we can just remove it 
                    if (oldAlias.AliasId != existingExactAlias.AliasId && oldAlias.AliasId > 0)
                    {
                        await context.Clients
                            .Where(_client => _client.CurrentAliasId == oldAlias.AliasId)
                            .ForEachAsync(_client => _client.CurrentAliasId = existingExactAlias.AliasId);

                        await context.SaveChangesAsync();

                        if (context.Entry(oldAlias).State != EntityState.Deleted)
                        {
                            _logger.LogDebug(
                                "[updatealias] {entity} has exact alias match, so we're going to try to remove aliasId {aliasId} with linkId {linkId}",
                                entity.ToString(), oldAlias.AliasId, oldAlias.LinkId);
                            context.Aliases.Remove(oldAlias);
                            await context.SaveChangesAsync();
                        }
                    }
                }

                // theres no exact match, but they've played before with the GUID or IP
                else
                {
                    _logger.LogDebug("[updatealias] {entity} is using a new alias", entity.ToString());

                    var newAlias = new EFAlias
                    {
                        DateAdded = DateTime.UtcNow,
                        IPAddress = ip,
                        LinkId = newAliasLink.AliasLinkId,
                        Name = name,
                        SearchableName = name.StripColors().ToLower(),
                        Active = true
                    };

                    entity.CurrentAlias = newAlias;
                    entity.CurrentAliasId = 0;
                    await context.SaveChangesAsync();
                }
            }
        }

        private async Task UpdateAliasNew(string originalName, int? ip, Data.Models.Client.EFClient entity,
            DatabaseContext context)
        {
            var name = originalName.CapClientName(EFAlias.MAX_NAME_LENGTH);

            var existingAliases = await context.Aliases
                .Where(alias => alias.Name == name && alias.LinkId == entity.AliasLinkId ||
                                alias.Name == name && alias.IPAddress != null && alias.IPAddress == ip)
                .ToListAsync();
            var defaultAlias = existingAliases.FirstOrDefault(alias => alias.IPAddress == null);
            var existingExactAlias =
                existingAliases.FirstOrDefault(alias => alias.IPAddress != null && alias.IPAddress == ip);

            if (defaultAlias != null && existingExactAlias == null)
            {
                defaultAlias.IPAddress = ip;
                entity.CurrentAlias = defaultAlias;
                entity.CurrentAliasId = defaultAlias.AliasId;
                await context.SaveChangesAsync();
                return;
            }

            if (existingExactAlias != null && entity.AliasLinkId == existingExactAlias.LinkId)
            {
                entity.CurrentAlias = existingExactAlias;
                entity.CurrentAliasId = existingExactAlias.AliasId;
                await context.SaveChangesAsync();
                _logger.LogDebug(
                    "[{Method}] client {Client} already has an existing exact alias, so we are not making changes",
                    nameof(UpdateAliasNew), entity.ToString());
                return;
            }

            _logger.LogDebug("[{Method}] {Entity} is using a new alias", nameof(UpdateAliasNew), entity.ToString());

            var newAlias = new EFAlias
            {
                DateAdded = DateTime.UtcNow,
                IPAddress = ip,
                LinkId = entity.AliasLinkId,
                Name = name,
                SearchableName = name.StripColors().ToLower(),
                Active = true
            };

            entity.CurrentAlias = newAlias;
            await context.SaveChangesAsync();
            entity.CurrentAliasId = newAlias.AliasId;
        }

        /// <summary>
        ///     updates the permission level of the given target to the given permission level
        /// </summary>
        /// <param name="newPermission"></param>
        /// <param name="temporalClient"></param>
        /// <param name="origin"></param>
        /// <returns></returns>
        public virtual async Task UpdateLevel(Permission newPermission, EFClient temporalClient, EFClient origin)
        {
            await using var context = _contextFactory.CreateContext();
            var entity = await context.Clients
                .Where(client => client.ClientId == temporalClient.ClientId)
                .FirstAsync();

            _logger.LogInformation("Updating {ClientId} from {OldPermission} to {NewPermission} ",
                temporalClient.ClientId, entity.Level, newPermission);

            entity.Level = newPermission;
            await context.SaveChangesAsync();
            temporalClient.Level = newPermission;
        }

        public async Task UpdateAlias(EFClient temporalClient)
        {
            await using var context = _contextFactory.CreateContext();

            var entity = context.Clients
                .Include(c => c.AliasLink)
                .Include(c => c.CurrentAlias)
                .First(e => e.ClientId == temporalClient.ClientId);

            if (_appConfig.EnableImplicitAccountLinking)
            {
                await UpdateAlias(temporalClient.Name, temporalClient.IPAddress, entity, context);
            }

            else
            {
                await UpdateAliasNew(temporalClient.Name, temporalClient.IPAddress, entity, context);
            }

            temporalClient.CurrentAlias = entity.CurrentAlias;
            temporalClient.CurrentAliasId = entity.CurrentAliasId;
            temporalClient.AliasLink = entity.AliasLink;
            temporalClient.AliasLinkId = entity.AliasLinkId;
        }

        /// <summary>
        ///     retrieves the number of times the given client id has been reported
        /// </summary>
        /// <param name="clientId">client id to search for report counts of</param>
        /// <returns></returns>
        public async Task<int> GetClientReportCount(int clientId)
        {
            await using var context = _contextFactory.CreateContext(false);
            return await context.Penalties
                .Where(_penalty => _penalty.Active)
                .Where(_penalty => _penalty.OffenderId == clientId)
                .Where(_penalty => _penalty.Type == EFPenalty.PenaltyType.Report)
                .CountAsync();
        }

        /// <summary>
        ///     indicates if the given clientid can be autoflagged
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public async Task<bool> CanBeAutoFlagged(int clientId)
        {
            await using var context = _contextFactory.CreateContext(false);

            var now = DateTime.UtcNow;
            var hasExistingAutoFlag = await context.Penalties
                .Where(_penalty => _penalty.Active)
                .Where(_penalty => _penalty.OffenderId == clientId)
                .Where(_penalty => _penalty.Type == EFPenalty.PenaltyType.Flag)
                .Where(_penalty => _penalty.PunisherId == 1)
                .Where(_penalty => _penalty.Expires == null || _penalty.Expires > now)
                .AnyAsync();

            var hasUnflag = await context.Penalties
                .Where(_penalty => _penalty.Active)
                .Where(_penalty => _penalty.OffenderId == clientId)
                .Where(_penalty => _penalty.Type == EFPenalty.PenaltyType.Unflag)
                .AnyAsync();

            return !hasExistingAutoFlag && !hasUnflag;
        }

        /// <summary>
        ///     Unlinks shared GUID account into its own separate account
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public async Task UnlinkClient(int clientId)
        {
            await using var ctx = _contextFactory.CreateContext();
            var newLink = new EFAliasLink { Active = true };
            ctx.AliasLinks.Add(newLink);
            await ctx.SaveChangesAsync();

            var client = await ctx.Clients.Include(_client => _client.CurrentAlias)
                .FirstAsync(_client => _client.ClientId == clientId);
            client.AliasLinkId = newLink.AliasLinkId;
            client.Level = Permission.User;

            await ctx.Aliases.Where(_alias =>
                    _alias.IPAddress == client.CurrentAlias.IPAddress && _alias.IPAddress != null)
                .ForEachAsync(_alias => _alias.LinkId = newLink.AliasLinkId);

            if (!_appConfig.EnableImplicitAccountLinking)
            {
                var clientIdsByIp = await ctx.Clients.Where(c =>
                        client.CurrentAlias.IPAddress != null &&
                        c.CurrentAlias.IPAddress == client.CurrentAlias.IPAddress)
                    .Select(c => c.ClientId)
                    .ToListAsync();

                await ctx.Penalties.Where(penalty =>
                        clientIdsByIp.Contains(penalty.OffenderId)
                        && new[]
                        {
                            EFPenalty.PenaltyType.Ban, EFPenalty.PenaltyType.TempBan, EFPenalty.PenaltyType.Flag
                        }.Contains(penalty.Type)
                        && penalty.Expires == null)
                    .ForEachAsync(penalty => penalty.Expires = DateTime.UtcNow);
            }

            await ctx.SaveChangesAsync();
        }

        #region ServiceSpecific

        public async Task<IList<EFClient>> GetOwners()
        {
            await using var context = _contextFactory.CreateContext(false);
            return await context.Clients
                .Where(c => c.Level == Permission.Owner)
                .Select(c => c.ToPartialClient())
                .ToListAsync();
        }

        public async Task<bool> HasOwnerAsync(CancellationToken token)
        {
            await using var context = _contextFactory.CreateContext(false);
            return await context.Clients.AnyAsync(client => client.Level == Permission.Owner, token);
        }

        /// <summary>
        ///     retrieves the number of owners
        ///     (client level is owner)
        /// </summary>
        /// <returns></returns>
        public virtual async Task<int> GetOwnerCount()
        {
            await using var context = _contextFactory.CreateContext(false);
            return await context.Clients
                .CountAsync(_client => _client.Level == Permission.Owner);
        }

        public async Task<EFClient> GetClientForLogin(int clientId)
        {
            await using var context = _contextFactory.CreateContext(false);
            return await context.Clients
                .Select(_client => new EFClient
                {
                    NetworkId = _client.NetworkId,
                    ClientId = _client.ClientId,
                    CurrentAlias = new EFAlias
                    {
                        Name = _client.CurrentAlias.Name
                    },
                    Password = _client.Password,
                    PasswordSalt = _client.PasswordSalt,
                    Level = _client.Level
                })
                .FirstAsync(_client => _client.ClientId == clientId);
        }

        public async Task<List<EFClient>> GetPrivilegedClients(bool includeName = true)
        {
            await using var context = _contextFactory.CreateContext(false);

            var iqClients = from client in context.Clients.AsNoTracking()
                where client.Level >= Permission.Trusted
                where client.Active
                select new EFClient
                {
                    AliasLinkId = client.AliasLinkId,
                    CurrentAlias = client.CurrentAlias,
                    ClientId = client.ClientId,
                    Level = client.Level,
                    Password = client.Password,
                    PasswordSalt = client.PasswordSalt,
                    NetworkId = client.NetworkId,
                    LastConnection = client.LastConnection
                };

            return await iqClients.ToListAsync();
        }

        public async Task<IList<PlayerInfo>> FindClientsByIdentifier(string identifier)
        {
            var trimmedIdentifier = identifier?.Trim();
            if (trimmedIdentifier == null || trimmedIdentifier.Length < _appConfig.MinimumNameLength)
            {
                return new List<PlayerInfo>();
            }

            await using var context = _contextFactory.CreateContext(false);
            long? networkId = null;
            try
            {
                networkId = trimmedIdentifier.ConvertGuidToLong(NumberStyles.HexNumber);
            }
            catch
            {
                // ignored
            }

            var ipAddress = trimmedIdentifier.ConvertToIP();

            var iqLinkIds = context.Aliases.Where(_alias => _alias.Active);

            // we want to query for the IP Address
            if (ipAddress != null && trimmedIdentifier.Split('.').Length == 3)
            {
                iqLinkIds = iqLinkIds.Where(_alias => _alias.IPAddress == ipAddress);
            }

            // want to find them by name (wildcard)
            else
            {
                iqLinkIds = iqLinkIds.Where(_alias => EF.Functions.Like(_alias.SearchableName ?? _alias.Name.ToLower(),
                    $"%{trimmedIdentifier.ToLower()}%") || EF.Functions.Like(_alias.SearchableIPAddress, $"{trimmedIdentifier}%"));
            }

            var linkIds = await iqLinkIds
                .Select(_alias => _alias.LinkId)
                .ToListAsync();

            // get all the clients that match the alias link or the network id
            var iqClients = context.Clients
                .Where(_client => _client.Active);

            var match = Regex.Match(trimmedIdentifier ?? "", "\"(.+)\"");

            if (match.Success)
            {
                iqClients = iqClients.Where(client =>
                    client.CurrentAlias.SearchableName.ToLower().Equals(match.Groups[1].ToString().ToLower()));
            }

            else
            {
                iqClients = iqClients.Where(_client => networkId == _client.NetworkId ||
                                                       linkIds.Contains(_client.AliasLinkId)
                                                       || !_appConfig.EnableImplicitAccountLinking &&
                                                       _client.CurrentAlias.IPAddress != null &&
                                                       _client.CurrentAlias.IPAddress == ipAddress);
            }

            // we want to project our results 
            var iqClientProjection = iqClients.OrderByDescending(_client => _client.LastConnection)
                .Select(_client => new PlayerInfo
                {
                    Name = _client.CurrentAlias.Name,
                    LevelInt = (int)_client.Level,
                    LastConnection = _client.LastConnection,
                    ClientId = _client.ClientId,
                    IPAddress = _client.CurrentAlias.IPAddress.HasValue
                        ? _client.CurrentAlias.SearchableIPAddress
                        : ""
                });

            var clients = await iqClientProjection.ToListAsync();

            // this is so we don't try to evaluate this in the linq to entities query
            foreach (var client in clients)
                client.Level = ((Permission)client.LevelInt).ToLocalizedLevelName();

            return clients;
        }

        public async Task<int> GetTotalClientsAsync()
        {
            await using var context = _contextFactory.CreateContext(false);
            return await context.Clients
                .CountAsync();
        }

        /// <summary>
        ///     Returns the number of clients seen today
        /// </summary>
        /// <returns></returns>
        public async Task<int> GetRecentClientCount()
        {
            await using var context = _contextFactory.CreateContext(false);
            var startOfPeriod = DateTime.UtcNow.AddHours(-24);
            var iqQuery = context.Clients.Where(_client => _client.LastConnection >= startOfPeriod);

            return await iqQuery.CountAsync();
        }

        /// <summary>
        ///     gets the 10 most recently added clients to IW4MAdmin
        /// </summary>
        /// <returns></returns>
        public async Task<IList<PlayerInfo>> GetRecentClients()
        {
            var startOfPeriod = DateTime.UtcNow.AddHours(-24);

            await using var context = _contextFactory.CreateContext(false);
            var iqClients = context.Clients
                .Where(_client => _client.CurrentAlias.IPAddress != null)
                .Where(_client => _client.FirstConnection >= startOfPeriod)
                .OrderByDescending(_client => _client.FirstConnection)
                .Select(_client => new PlayerInfo
                {
                    ClientId = _client.ClientId,
                    Name = _client.CurrentAlias.Name,
                    IPAddress = _client.CurrentAlias.IPAddress.ConvertIPtoString(),
                    LastConnection = _client.FirstConnection
                });

            return await iqClients.ToListAsync();
        }

        #endregion
    }
}
