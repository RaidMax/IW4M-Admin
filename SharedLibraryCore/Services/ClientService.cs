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
                    // the first time a client is created, we may not have their ip, 
                    // so we create a temporary alias
                    Active = false
                };

                context.Clients.Add(client);
                await context.SaveChangesAsync();

                return client;
            }
        }

        public async Task UpdateAlias(EFClient entity)
        {
            // todo: move this out
            if (entity.IsBot)
            {
                return;
            }

            using (var context = new DatabaseContext())
            {
                context.Attach(entity);

                string name = entity.Name;
                int? ip = entity.IPAddress;

                // indicates if someone appears to have played before
                bool hasExistingAlias = false;

                // get all aliases by IP address and LinkId
                var iqAliases = context.Aliases
                    .Include(a => a.Link)
                    .Where(a => a.Link.Active)
                    .Where(a => (a.IPAddress != null && a.IPAddress == ip) ||
                        a.LinkId == entity.AliasLinkId);

#if DEBUG == true
                var aliasSql = iqAliases.ToSql();
#endif
                var aliases = await iqAliases.ToListAsync();

                // see if they have a matching IP + Name but new NetworkId
                var existingAlias = aliases.FirstOrDefault(a => a.Name == name && a.IPAddress == ip);
                bool exactAliasMatch = existingAlias != null;
                // if existing alias matches link them
                EFAliasLink aliasLink = existingAlias?.Link;
                // if no exact matches find the first IP that matches
                aliasLink = aliasLink ?? aliases.FirstOrDefault()?.Link;
                // if no matches are found, create new link
                aliasLink = aliasLink ?? new EFAliasLink();

                hasExistingAlias = aliases.Count > 0;

                // the existing alias matches ip and name, so we can just ignore the temporary one
                if (exactAliasMatch)
                {
                    entity.CurrentServer.Logger.WriteDebug($"{entity} has exact alias match");
                    // they're using the same alias as before, so we need to make sure the current aliases is set to it
                    if (entity.CurrentAliasId != existingAlias.AliasId)
                    {
                        context.Update(entity);

                        entity.CurrentAlias = existingAlias;
                        if (existingAlias.AliasId > 0)
                        {
                            entity.CurrentAliasId = existingAlias.AliasId;
                        }
                        else
                        {
                            entity.CurrentServer.Logger.WriteDebug($"Updating alias for {entity} failed");
                        }
                        await context.SaveChangesAsync();
                    }
                }

                // theres no exact match, but they've played before with the GUID or IP
                else if (hasExistingAlias)
                {
                    context.Update(entity);
                    entity.AliasLink = aliasLink;
                    entity.AliasLinkId = aliasLink.AliasLinkId;

                    // the current link is temporary so we need to update
                    if (!entity.AliasLink.Active)
                    {
                        entity.CurrentServer.Logger.WriteDebug($"{entity} has temporary alias so we are deleting");
                        
                        // we want to delete the temporary alias link
                        context.Entry(entity.AliasLink).State = EntityState.Deleted;
                        entity.AliasLink = null;
                        await context.SaveChangesAsync();
                    }

                    entity.CurrentServer.Logger.WriteDebug($"Connecting player is using a new alias {entity}");

                    var newAlias = new EFAlias()
                    {
                        DateAdded = DateTime.UtcNow,
                        IPAddress = ip,
                        Link = aliasLink,
                        Name = name
                    };

                    context.Aliases.Add(newAlias);
                    entity.CurrentAlias = newAlias;

                    await context.SaveChangesAsync();
                }

                // no record of them playing
                else
                {
                    entity.CurrentServer.Logger.WriteDebug($"{entity} has not be seen before");
                    var newAlias = new EFAlias()
                    {
                        DateAdded = DateTime.UtcNow,
                        IPAddress = ip,
                        Link = entity.AliasLink,
                        Name = name
                    };

                    context.Update(entity);
                    context.Update(entity.AliasLink);

                    entity.AliasLink.Active = true;
                    context.Aliases.Add(newAlias);
                    entity.CurrentAlias = newAlias;

                    await context.SaveChangesAsync();
                }

                var linkIds = aliases.Select(a => a.LinkId);

                if (linkIds.Count() > 0)
                {
                    var highestLevel = await context.Clients
                        .Where(c => linkIds.Contains(c.AliasLinkId))
                        .MaxAsync(c => c.Level);

                    if (entity.Level != highestLevel)
                    {
                        // todo: log level changes here
                        context.Update(entity);
                        entity.Level = highestLevel;
                        await context.SaveChangesAsync();
                    }
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

        public async Task<EFClient> Update(EFClient entity)
        {
            using (var context = new DatabaseContext())
            {
                // grab the context version of the entity
                var client = context.Clients
                    .Include(c => c.AliasLink)
                    .Include(c => c.CurrentAlias)
                    .First(e => e.ClientId == entity.ClientId);

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
                        c.Level = entity.Level;
                    });
                }

                // their alias has been updated and not yet saved
                if (entity.CurrentAlias.AliasId == 0)
                {
                    client.CurrentAlias = new EFAlias()
                    {
                        Active = entity.CurrentAlias.IPAddress.HasValue ? true : false,
                        DateAdded = DateTime.UtcNow,
                        IPAddress = entity.CurrentAlias.IPAddress,
                        Name = entity.CurrentAlias.Name,
                        Link = client.AliasLink
                    };
                }

                else
                {
                    client.CurrentAliasId = entity.CurrentAliasId;
                    client.IPAddress = entity.IPAddress;
                    client.Name = entity.Name;
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
                                    PasswordSalt = client.PasswordSalt
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
