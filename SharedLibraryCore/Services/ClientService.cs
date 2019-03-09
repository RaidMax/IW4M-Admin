using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Objects;
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
                var client = new EFClient()
                {
                    Level = Permission.User,
                    FirstConnection = DateTime.UtcNow,
                    Connections = 1,
                    LastConnection = DateTime.UtcNow,
                    Masked = false,
                    NetworkId = entity.NetworkId,
                    AliasLink = new EFAliasLink()
                    {
                        Active = false
                    },
                    ReceivedPenalties = new List<EFPenalty>()
                };

                client.CurrentAlias = new Alias()
                {
                    Name = entity.Name,
                    Link = client.AliasLink,
                    DateAdded = DateTime.UtcNow,
                    IPAddress = entity.IPAddress,
                    // the first time a client is created, we may not have their ip, 
                    // so we create a temporary alias
                    Active = false
                };

                context.Clients.Add(client);
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
                .Where(a => (a.IPAddress == ip) ||
                    a.LinkId == entity.AliasLinkId);

#if DEBUG == true
            var aliasSql = iqAliases.ToSql();
#endif
            var aliases = await iqAliases.ToListAsync();

            // see if they have a matching IP + Name but new NetworkId
            var existingExactAlias = aliases.FirstOrDefault(a => a.Name == name && a.IPAddress == ip);
            bool exactAliasMatch = existingExactAlias != null;

            // if existing alias matches link them
            EFAliasLink aliasLink = existingExactAlias?.Link;
            // if no exact matches find the first IP that matches
            aliasLink = aliasLink ?? aliases.FirstOrDefault()?.Link;
            // if no matches are found, use our current one
            aliasLink = aliasLink ?? entity.AliasLink;

            bool hasExistingAlias = aliases.Count > 0;

            // this happens when an alias exists but the current link is a temporary one
            if ((exactAliasMatch || hasExistingAlias) &&
                (!entity.AliasLink.Active && entity.AliasLinkId != aliasLink.AliasLinkId))
            {
                entity.AliasLinkId = aliasLink.AliasLinkId;
                entity.AliasLink = aliasLink;

                //entity.CurrentServer.Logger.WriteDebug($"Updating alias link for {entity}");
                await context.SaveChangesAsync();

                foreach (var alias in aliases.Append(entity.CurrentAlias)
                    .Where(_alias => !_alias.Active ||
                    _alias.LinkId != aliasLink.AliasLinkId))
                {
                    entity.CurrentServer.Logger.WriteDebug($"{entity} updating alias-link id is {alias.LinkId}");
                    alias.Active = true;
                    alias.LinkId = aliasLink.AliasLinkId;
                }

                //entity.CurrentServer.Logger.WriteDebug($"Saving updated aliases for {entity}");
                await context.SaveChangesAsync();

                // todo: fix this
                /*context.AliasLinks.Remove(entity.AliasLink);
                entity.AliasLink = null;

                //entity.CurrentServer.Logger.WriteDebug($"Removing temporary link for {entity}");

                try
                {
                    await context.SaveChangesAsync();
                }
                catch
                {
                   // entity.CurrentServer.Logger.WriteDebug($"Failed to remove link for {entity}");
                }*/
            }

            // the existing alias matches ip and name, so we can just ignore the temporary one
            if (exactAliasMatch)
            {
                entity.CurrentServer.Logger.WriteDebug($"{entity} has exact alias match");
                entity.CurrentAliasId = existingExactAlias.AliasId;
                entity.CurrentAlias = existingExactAlias;
                await context.SaveChangesAsync();
            }

            // theres no exact match, but they've played before with the GUID or IP
            else if (hasExistingAlias)
            {
                //entity.CurrentServer.Logger.WriteDebug($"Connecting player is using a new alias {entity}");

                // this happens when a temporary alias gets updated
                if (entity.CurrentAlias.Name == name && entity.CurrentAlias.IPAddress == null)
                {
                    entity.CurrentAlias.IPAddress = ip;
                    entity.CurrentAlias.Active = true;
                    //entity.CurrentServer.Logger.WriteDebug($"Updating temporary alias for {entity}");
                    await context.SaveChangesAsync();
                }

                else
                {
                    var newAlias = new EFAlias()
                    {
                        DateAdded = DateTime.UtcNow,
                        IPAddress = ip,
                        LinkId = aliasLink.AliasLinkId,
                        Name = name,
                        Active = true,
                    };

                    entity.CurrentAlias = newAlias;
                    //entity.CurrentServer.Logger.WriteDebug($"Saving new alias for {entity}");
                    await context.SaveChangesAsync();
                }
            }

            // no record of them playing
            else
            {
                //entity.CurrentServer.Logger.WriteDebug($"{entity} has not be seen before");

                entity.AliasLink.Active = true;
                entity.CurrentAlias.Active = true;
                entity.CurrentAlias.IPAddress = ip;
                entity.CurrentAlias.Name = name;

                //entity.CurrentServer.Logger.WriteDebug($"updating new alias for {entity}");
                await context.SaveChangesAsync();
            }

            var linkIds = aliases.Select(a => a.LinkId);

            if (linkIds.Count() > 0 &&
                aliases.Count(_alias => _alias.Name == name && _alias.IPAddress == ip) > 0)
            {
                var highestLevel = await context.Clients
                    .Where(c => linkIds.Contains(c.AliasLinkId))
                    .MaxAsync(c => c.Level);

                if (entity.Level != highestLevel)
                {
                    entity.CurrentServer.Logger.WriteDebug($"{entity} updating user level");
                    // todo: log level changes here
                    context.Update(entity);
                    entity.Level = highestLevel;
                    await context.SaveChangesAsync();
                }
            }
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

        public async Task<IList<EFClient>> Find(Func<EFClient, bool> e)
        {
            return await Task.Run(() =>
            {
                using (var context = new DatabaseContext(true))
                {
                    return context.Clients
                         .Include(c => c.CurrentAlias)
                         .Include(c => c.AliasLink.Children)
                         .Where(e).ToList();
                }
            });
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

        public async Task UpdateAlias(EFClient entity)
        {
            using (var context = new DatabaseContext())
            {
                var client = context.Clients
                    .Include(c => c.AliasLink)
                    .Include(c => c.CurrentAlias)
                    .First(e => e.ClientId == entity.ClientId);

                client.CurrentServer = entity.CurrentServer;

                await UpdateAlias(entity.Name, entity.IPAddress, client, context);

                entity.CurrentAlias = client.CurrentAlias;
                entity.CurrentAliasId = client.CurrentAliasId;
                entity.AliasLink = client.AliasLink;
                entity.AliasLinkId = client.AliasLinkId;
            }
        }

        public async Task<EFClient> Update(EFClient entity)
        {
            using (var context = new DatabaseContext())
            {
                // grab the context version of the entity
                var client = context.Clients
                    .First(e => e.ClientId == entity.ClientId);

                client.CurrentServer = entity.CurrentServer;

                // if their level has been changed
                if (entity.Level != client.Level)
                {
                    // get all clients that use the same aliasId
                    var matchingClients = context.Clients
                        .Where(c => c.CurrentAliasId == client.CurrentAliasId)
                        // make sure we don't select ourselves twice
                        .Where(c => c.ClientId != entity.ClientId);

                    // update all related clients level
                    await matchingClients.ForEachAsync(c =>
                    {
                        // todo: log that it has changed here
                        c.Level = entity.Level;
                    });
                }

                // set remaining non-navigation properties that may have been updated
                client.Level = entity.Level;
                client.LastConnection = entity.LastConnection;
                client.Connections = entity.Connections;
                client.FirstConnection = entity.FirstConnection;
                client.Masked = entity.Masked;
                client.TotalConnectionTime = entity.TotalConnectionTime;
                client.Password = entity.Password;
                client.PasswordSalt = entity.PasswordSalt;

                // update in database
                await context.SaveChangesAsync();

                // this is set so future updates don't trigger a new alias add
                if (entity.CurrentAlias.AliasId == 0)
                {
                    entity.CurrentAlias.AliasId = client.CurrentAlias.AliasId;
                }

                return client;
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

        public async Task<List<EFClient>> GetPrivilegedClients()
        {
            using (var context = new DatabaseContext(disableTracking: true))
            {
                var iqClients = from client in context.Clients
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
                                    NetworkId = client.NetworkId
                                };

#if DEBUG == true
                var clientsSql = iqClients.ToSql();
#endif

                return await iqClients.ToListAsync();
            }
        }

        public async Task<IList<EFClient>> FindClientsByIdentifier(string identifier)
        {
            if (identifier.Length < 3)
            {
                return new List<EFClient>();
            }

            identifier = identifier.ToLower();

            using (var context = new DatabaseContext(disableTracking: true))
            {
                long networkId = identifier.ConvertLong();
                int? ipAddress = identifier.ConvertToIP();

                var iqLinkIds = (from alias in context.Aliases
                                 where (alias.IPAddress != null && alias.IPAddress == ipAddress) ||
                                alias.Name.ToLower().Contains(identifier)
                                 select alias.LinkId).Distinct();

                var linkIds = iqLinkIds.ToList();

                var iqClients = context.Clients
                    .Where(c => linkIds.Contains(c.AliasLinkId) ||
                        networkId == c.NetworkId)
                    .Include(c => c.CurrentAlias)
                    .Include(c => c.AliasLink.Children);

#if DEBUG == true
                var iqClientsSql = iqClients.ToSql();
#endif

                return await iqClients.ToListAsync();
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

        public Task<EFClient> CreateProxy()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
