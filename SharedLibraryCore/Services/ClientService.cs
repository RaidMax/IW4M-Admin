using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static SharedLibraryCore.Database.Models.EFClient;

namespace SharedLibraryCore.Services
{

    public class ClientService : Interfaces.IEntityService<EFClient>
    {
        public async Task<EFClient> Create(EFClient entity)
        {
            using (var context = new DatabaseContext())
            {
                int? linkId = null;
                int? aliasId = null;

                if (entity.IPAddress != null)
                {
                    var existingAlias = await context.Aliases
                        .Select(_alias => new { _alias.AliasId, _alias.LinkId, _alias.IPAddress, _alias.Name })
                        .FirstOrDefaultAsync(_alias => _alias.IPAddress == entity.IPAddress);

                    if (existingAlias != null)
                    {
                        entity.CurrentServer.Logger.WriteDebug($"[create] client with new GUID {entity} has existing link {existingAlias.LinkId}");

                        linkId = existingAlias.LinkId;
                        if (existingAlias.Name == entity.Name)
                        {
                            entity.CurrentServer.Logger.WriteDebug($"[create] client with new GUID {entity} has existing alias {existingAlias.AliasId}");
                            aliasId = existingAlias.AliasId;
                        }
                    }
                }

                var client = new EFClient()
                {
                    Level = Permission.User,
                    FirstConnection = DateTime.UtcNow,
                    LastConnection = DateTime.UtcNow,
                    NetworkId = entity.NetworkId
                };

                context.Clients.Add(client);

                // they're just using a new GUID
                if (aliasId.HasValue)
                {
                    entity.CurrentServer.Logger.WriteDebug($"[create] setting {entity}'s alias id and linkid to ({aliasId.Value}, {linkId.Value})");
                    client.CurrentAliasId = aliasId.Value;
                    client.AliasLinkId = linkId.Value;
                }

                // link was found but they don't have an exact alias
                else if (!aliasId.HasValue && linkId.HasValue)
                {
                    entity.CurrentServer.Logger.WriteDebug($"[create] setting {entity}'s linkid to {linkId.Value}, but creating new alias");
                    client.AliasLinkId = linkId.Value;
                    client.CurrentAlias = new EFAlias()
                    {
                        Name = entity.Name,
                        DateAdded = DateTime.UtcNow,
                        IPAddress = entity.IPAddress,
                        LinkId = linkId.Value
                    };
                }

                // brand new players (supposedly)
                else
                {
                    entity.CurrentServer.Logger.WriteDebug($"[create] creating new Link and Alias for {entity}");
                    var link = new EFAliasLink();
                    var alias = new EFAlias()
                    {
                        Name = entity.Name,
                        DateAdded = DateTime.UtcNow,
                        IPAddress = entity.IPAddress,
                        Link = link
                    };

                    link.Children.Add(alias);

                    client.AliasLink = link;
                    client.CurrentAlias = alias;
                }

                await context.SaveChangesAsync();

                return client;
            }
        }

        private async Task UpdateAlias(string name, int? ip, EFClient entity, DatabaseContext context)
        {
            // entity is the tracked db context item
            // get all aliases by IP address and LinkId
            var iqAliases = context.Aliases
                .Include(a => a.Link)
                // we only want alias that have the same IP address or share a link
                .Where(_alias => _alias.IPAddress == ip || (_alias.LinkId == entity.AliasLinkId));

#if DEBUG == true
            var aliasSql = iqAliases.ToSql();
#endif
            var aliases = await iqAliases.ToListAsync();
          
            // see if they have a matching IP + Name but new NetworkId
            var existingExactAlias = aliases.FirstOrDefault(a => a.Name == name && a.IPAddress == ip);
            bool hasExactAliasMatch = existingExactAlias != null;

            // if existing alias matches link them
            var newAliasLink = existingExactAlias?.Link;
            // if no exact matches find the first IP or LinkId that matches
            newAliasLink = newAliasLink ?? aliases.OrderBy(_alias => _alias.LinkId).FirstOrDefault()?.Link;
            // if no matches are found, use our current one ( it will become permanent )
            newAliasLink = newAliasLink ?? entity.AliasLink;

            bool hasExistingAlias = aliases.Count > 0;
            bool isAliasLinkUpdated = newAliasLink.AliasLinkId != entity.AliasLink.AliasLinkId;

            // update each of the aliases where this is no IP but the name is identical
            foreach (var alias in aliases.Where(_alias => (_alias.IPAddress == null || _alias.IPAddress == 0)))
            {
                alias.IPAddress = ip;
            }

            await context.SaveChangesAsync();

            // this happens when the link we found is different than the one we create before adding an IP
            if (isAliasLinkUpdated)
            {
                entity.CurrentServer.Logger.WriteDebug($"[updatealias] found a link for {entity} so we are updating link from {entity.AliasLink.AliasLinkId} to {newAliasLink.AliasLinkId}");

                var oldAliasLink = entity.AliasLink;

                // update all the clients that have the old alias link
                await context.Clients
                    .Where(_client => _client.AliasLinkId == oldAliasLink.AliasLinkId)
                    .ForEachAsync(_client => _client.AliasLinkId = newAliasLink.AliasLinkId);

                entity.AliasLink = newAliasLink;
                entity.AliasLinkId = newAliasLink.AliasLinkId;

                // update all previous aliases
                await context.Aliases
                    .Where(_alias => _alias.LinkId == oldAliasLink.AliasLinkId)
                    .ForEachAsync(_alias => _alias.LinkId = newAliasLink.AliasLinkId);

                await context.SaveChangesAsync();
                // we want to delete the now inactive alias
                context.AliasLinks.Remove(oldAliasLink);
                await context.SaveChangesAsync();
            }

            // the existing alias matches ip and name, so we can just ignore the temporary one
            if (hasExactAliasMatch)
            {
                entity.CurrentServer.Logger.WriteDebug($"[updatealias] {entity} has exact alias match");

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

                    entity.CurrentServer.Logger.WriteDebug($"[updatealias] {entity} has exact alias match, so we're going to try to remove aliasId {oldAlias.AliasId} with linkId {oldAlias.AliasId}");
                    context.Aliases.Remove(oldAlias);
                    await context.SaveChangesAsync();
                }
            }

            // theres no exact match, but they've played before with the GUID or IP
            else
            {
                entity.CurrentServer.Logger.WriteDebug($"[updatealias] {entity} is using a new alias");

                var newAlias = new EFAlias()
                {
                    DateAdded = DateTime.UtcNow,
                    IPAddress = ip,
                    LinkId = newAliasLink.AliasLinkId,
                    Name = name,
                    Active = true,
                };

                entity.CurrentAlias = newAlias;
                entity.CurrentAliasId = 0;
                await context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// updates the permission level of the given target to the given permission level
        /// </summary>
        /// <param name="newPermission"></param>
        /// <param name="temporalClient"></param>
        /// <param name="origin"></param>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public async Task UpdateLevel(Permission newPermission, EFClient temporalClient, EFClient origin)
        {
            using (var ctx = new DatabaseContext())
            {
                var entity = await ctx.Clients
                    .Where(_client => _client.ClientId == temporalClient.ClientId)
                    .FirstAsync();

                var oldPermission = entity.Level;

                entity.Level = newPermission;
                await ctx.SaveChangesAsync();

#if DEBUG == true
                temporalClient.CurrentServer.Logger.WriteDebug($"Updated {temporalClient.ClientId} to {newPermission}");
#endif

                // if their permission level has been changed to level that needs to be updated on all accounts
                if ((oldPermission != newPermission) &&
                    (newPermission == Permission.Banned ||
                     newPermission == Permission.Flagged ||
                     newPermission == Permission.User))
                {
                    var changeSvc = new ChangeHistoryService();

                    //get all clients that have the same linkId
                    var iqMatchingClients = ctx.Clients
                        .Where(_client => _client.AliasLinkId == entity.AliasLinkId);
                    // make sure we don't select ourselves twice
                    //.Where(_client => _client.ClientId != temporalClient.ClientId);

                    // this updates the level for all the clients with the same LinkId
                    // only if their new level is flagged or banned
                    await iqMatchingClients.ForEachAsync(_client =>
                    {
                        _client.Level = newPermission;
#if DEBUG == true
                        temporalClient.CurrentServer.Logger.WriteDebug($"Updated linked {_client.ClientId} to {newPermission}");
#endif
                    });

                    await ctx.SaveChangesAsync();
                }
            }

            temporalClient.Level = newPermission;
        }

        public async Task<EFClient> Delete(EFClient entity)
        {
            using (var context = new DatabaseContext())
            {
                var client = context.Clients
                    .Single(e => e.ClientId == entity.ClientId);
                entity.Active = false;
                context.Entry(entity).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return entity;
            }
        }

        public Task<IList<EFClient>> Find(Func<EFClient, bool> e)
        {
            throw new NotImplementedException();
        }

        public async Task<EFClient> Get(int entityID)
        {
            using (var context = new DatabaseContext(true))
            {
                var iqClient = from client in context.Clients
                               .Include(c => c.CurrentAlias)
                               .Include(c => c.AliasLink.Children)
                               .Include(c => c.Meta)
                               where client.ClientId == entityID
                               select new
                               {
                                   Client = client,
                                   LinkedAccounts = (from linkedClient in context.Clients
                                                     where client.AliasLinkId == linkedClient.AliasLinkId
                                                     select new
                                                     {
                                                         linkedClient.ClientId,
                                                         linkedClient.NetworkId
                                                     })
                               };
#if DEBUG == true
                var clientSql = iqClient.ToSql();
#endif
                var foundClient = await iqClient.FirstOrDefaultAsync();

                if (foundClient == null)
                {
                    return null;
                }

                foundClient.Client.LinkedAccounts = new Dictionary<int, long>();
                // todo: find out the best way to do this
                // I'm doing this here because I don't know the best way to have multiple awaits in the query
                foreach (var linked in foundClient.LinkedAccounts)
                {
                    foundClient.Client.LinkedAccounts.Add(linked.ClientId, linked.NetworkId);
                }

                return foundClient.Client;
            }
        }

        private static readonly Func<DatabaseContext, long, Task<EFClient>> _getUniqueQuery =
            EF.CompileAsyncQuery((DatabaseContext context, long networkId) =>
                context.Clients
                .Include(c => c.CurrentAlias)
                .Include(c => c.AliasLink.Children)
                .Include(c => c.ReceivedPenalties)
                .FirstOrDefault(c => c.NetworkId == networkId)
        );

        public async Task<EFClient> GetUnique(long entityAttribute)
        {
            using (var context = new DatabaseContext(true))
            {
                return await _getUniqueQuery(context, entityAttribute);
            }
        }

        public async Task UpdateAlias(EFClient temporalClient)
        {
            using (var context = new DatabaseContext())
            {
                var entity = context.Clients
                    .Include(c => c.AliasLink)
                    .Include(c => c.CurrentAlias)
                    .First(e => e.ClientId == temporalClient.ClientId);

                entity.CurrentServer = temporalClient.CurrentServer;

                await UpdateAlias(temporalClient.Name, temporalClient.IPAddress, entity, context);

                temporalClient.CurrentAlias = entity.CurrentAlias;
                temporalClient.CurrentAliasId = entity.CurrentAliasId;
                temporalClient.AliasLink = entity.AliasLink;
                temporalClient.AliasLinkId = entity.AliasLinkId;
            }
        }

        public async Task<EFClient> Update(EFClient temporalClient)
        {
            using (var context = new DatabaseContext())
            {
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
                return entity;
            }
        }

        #region ServiceSpecific
        public async Task<IList<EFClient>> GetOwners()
        {
            using (var context = new DatabaseContext())
            {
                return await context.Clients
                    .Where(c => c.Level == Permission.Owner)
                    .ToListAsync();
            }
        }

        /// <summary>
        /// retrieves the number of owners 
        /// (client level is owner)
        /// </summary>
        /// <returns></returns>
        public async Task<int> GetOwnerCount()
        {
            using (var ctx = new DatabaseContext(true))
            {
                return await ctx.Clients
                    .CountAsync(_client => _client.Level == Permission.Owner);
            }
        }

        public async Task<List<EFClient>> GetPrivilegedClients(bool includeName = true)
        {
            using (var context = new DatabaseContext(disableTracking: true))
            {
                var iqClients = from client in context.Clients.AsNoTracking()
                                where client.Level >= Permission.Trusted
                                where client.Active
                                select new EFClient()
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

#if DEBUG == true
                var clientsSql = iqClients.ToSql();
#endif

                return await iqClients.ToListAsync();
            }
        }

        public async Task<IList<PlayerInfo>> FindClientsByIdentifier(string identifier)
        {
            if (identifier?.Length < 3)
            {
                return new List<PlayerInfo>();
            }

            using (var context = new DatabaseContext(disableTracking: true))
            {
                long? networkId = null;
                try
                {
                    networkId = identifier.ConvertGuidToLong();
                }
                catch { }

                int? ipAddress = identifier.ConvertToIP();

                IQueryable<EFAlias> iqLinkIds = context.Aliases.Where(_alias => _alias.Active);

                // we want to query for the IP ADdress
                if (ipAddress != null)
                {
                    iqLinkIds = iqLinkIds.Where(_alias => _alias.IPAddress == ipAddress);
                }

                // want to find them by name (wildcard)
                else
                {
                    iqLinkIds = iqLinkIds.Where(_alias => EF.Functions.Like(_alias.Name.ToLower(), $"%{identifier.ToLower()}%"));
                }

                var linkIds = await iqLinkIds
                    .Select(_alias => _alias.LinkId)
                    .ToListAsync();

                // get all the clients that match the alias link or the network id
                var iqClients = context.Clients
                    .Where(_client => _client.Active);

                if (networkId.HasValue)
                {
                    iqClients = iqClients.Where(_client => networkId.Value == _client.NetworkId);
                }

                else
                {
                    iqClients = iqClients.Where(_client => linkIds.Contains(_client.AliasLinkId));
                }

                // we want to project our results 
                var iqClientProjection = iqClients.OrderByDescending(_client => _client.LastConnection)
                    .Select(_client => new PlayerInfo()
                    {
                        Name = _client.CurrentAlias.Name,
                        LevelInt = (int)_client.Level,
                        LastConnection = _client.LastConnection,
                        ClientId = _client.ClientId,
                    });
#if DEBUG == true
                var iqClientsSql = iqClients.ToSql();
#endif
                var clients = await iqClientProjection.ToListAsync();

                // this is so we don't try to evaluate this in the linq to entities query
                foreach (var client in clients)
                {
                    client.Level = ((Permission)client.LevelInt).ToLocalizedLevelName();
                }

                return clients;
            }
        }

        public async Task<int> GetTotalClientsAsync()
        {
            using (var context = new DatabaseContext(true))
            {
                return await context.Clients
                    .CountAsync();
            }
        }
        #endregion
    }
}
