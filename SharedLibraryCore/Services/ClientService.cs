using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using SharedLibraryCore.Database;
using SharedLibraryCore.Database.Models;
using System.Linq.Expressions;
using SharedLibraryCore.Objects;
using Microsoft.EntityFrameworkCore;

namespace SharedLibraryCore.Services
{

    public class ClientService : Interfaces.IEntityService<EFClient>
    {
        public async Task<EFClient> Create(EFClient entity)
        {
            using (var context = new DatabaseContext())
            {
                bool hasExistingAlias = false;
                // get all aliases by IP
                var aliases = await context.Aliases
                    .Include(a => a.Link)
                    .Where(a => a.IPAddress == entity.IPAddress)
                    .ToListAsync();

                // see if they have a matching IP + Name but new NetworkId
                var existingAlias = aliases.FirstOrDefault(a => a.Name == entity.Name);
                // if existing alias matches link them
                EFAliasLink aliasLink = existingAlias?.Link;
                // if no exact matches find the first IP that matches
                aliasLink = aliasLink ?? aliases.FirstOrDefault()?.Link;
                // if no exact or IP matches, create new link
                aliasLink = aliasLink ?? new EFAliasLink()
                {
                    Active = true,
                };

                // this has to be set here because we can't evalute it properly later
                hasExistingAlias = existingAlias != null;

                // if no existing alias create new alias
                existingAlias = existingAlias ?? new EFAlias()
                {
                    Active = true,
                    DateAdded = DateTime.UtcNow,
                    IPAddress = entity.IPAddress,
                    Link = aliasLink,
                    Name = entity.Name,
                };

                var client = new EFClient()
                {
                    Active = true,
                    // set the level to the level of the existing client if they have the same IP + Name but new NetworkId
                    // fixme: issues?
                    Level = hasExistingAlias ?
                        (await context.Clients.Where(c => c.AliasLinkId == existingAlias.LinkId)
                        .OrderByDescending(c => c.Level)
                        .FirstOrDefaultAsync())?.Level ?? Player.Permission.User :
                        Player.Permission.User,
                    FirstConnection = DateTime.UtcNow,
                    Connections = 1,
                    LastConnection = DateTime.UtcNow,
                    Masked = false,
                    NetworkId = entity.NetworkId,
                    AliasLink = aliasLink,
                    CurrentAlias = existingAlias,
                };

                context.Clients.Add(client);
                await context.SaveChangesAsync();

                return client;
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
                using (var context = new DatabaseContext())
                {
                    return context.Clients
                          .AsNoTracking()
                         .Include(c => c.CurrentAlias)
                         .Include(c => c.AliasLink.Children)
                         .Where(e).ToList();
                }
            });
        }

        public async Task<EFClient> Get(int entityID)
        {
            using (var context = new DatabaseContext())
            {
                context.ChangeTracker.AutoDetectChangesEnabled = false;
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

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
                var foundClient = await iqClient.FirstOrDefaultAsync();

                if (foundClient == null)
                    return null;

                foundClient.Client.LinkedAccounts = new Dictionary<int, long>();
                // todo: find out the best way to do this
                // I'm doing this here because I don't know the best way to have multiple awaits in the query
                foreach (var linked in foundClient.LinkedAccounts)
                    foundClient.Client.LinkedAccounts.Add(linked.ClientId, linked.NetworkId);

                return foundClient.Client;
            }
        }

        public async Task<EFClient> GetUnique(long entityAttribute)
        {
            using (var context = new DatabaseContext())
            {
                return await context.Clients
                    .AsNoTracking()
                    .Include(c => c.CurrentAlias)
                    .Include(c => c.AliasLink.Children)
                    .SingleOrDefaultAsync(c => c.NetworkId == entityAttribute);
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
                    .Single(e => e.ClientId == entity.ClientId);
               
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
                        Active = true,
                        DateAdded = DateTime.UtcNow,
                        IPAddress = entity.CurrentAlias.IPAddress,
                        Name = entity.CurrentAlias.Name,
                        Link = client.AliasLink
                    };
                }

                else
                {
                    client.CurrentAliasId = entity.CurrentAliasId;
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
                    entity.CurrentAlias.AliasId = client.CurrentAlias.AliasId;
                return client;
            }
        }

        #region ServiceSpecific
        public async Task<IList<EFClient>> GetOwners()
        {
            using (var context = new DatabaseContext())
                return await context.Clients
                    .Where(c => c.Level == Player.Permission.Owner)
                    .ToListAsync();
        }

        public async Task<bool> IsAuthenticated(int clientIP)
        {
            using (var context = new DatabaseContext())
            {
                var iqMatching = from alias in context.Aliases
                                 where alias.IPAddress == clientIP
                                 join client in context.Clients
                                 on alias.LinkId equals client.AliasLinkId
                                 where client.Level > Player.Permission.Trusted
                                 select client;

                return (await iqMatching.CountAsync()) > 0;
            }
        }

        public async Task<IList<EFClient>> GetPrivilegedClients()
        {
            using (var context = new DatabaseContext())
            {
                context.ChangeTracker.AutoDetectChangesEnabled = false;
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

                var iqClients = from client in context.Clients
                                .Include(c => c.CurrentAlias)
                                where client.Level >= Player.Permission.Trusted
                                select client;

                return await iqClients.ToListAsync();
            }
        }

        public async Task<IList<EFClient>> GetClientByName(string name)
        {
            if (name.Length < 3)
                return new List<EFClient>();

            using (var context = new DatabaseContext())
            {
                context.ChangeTracker.AutoDetectChangesEnabled = false;
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

                int asIP = name.ConvertToIP();
                // hack: so IW4MAdmin doesn't show up in search results
                asIP = asIP == 0 ? int.MinValue : asIP;

                var iqClients = (from alias in context.Aliases
                       .AsNoTracking()
                                 where alias.Name.ToLower()
                                     .Contains(name.ToLower()) ||
                                     alias.IPAddress == asIP
                                 join link in context.AliasLinks
                                 on alias.LinkId equals link.AliasLinkId
                                 join client in context.Clients
                                     .AsNoTracking()
                                 on alias.LinkId equals client.AliasLinkId
                                 select client)
                       .Distinct()
                       .Include(c => c.CurrentAlias)
                       .Include(c => c.AliasLink.Children);

                return await iqClients.ToListAsync();
            }
        }

        public async Task<IList<EFClient>> GetClientByIP(int ipAddress)
        {
            using (var context = new DatabaseContext())
            {
                var iqClients = (from alias in context.Aliases
                       .AsNoTracking()
                                 where alias.IPAddress == ipAddress
                                 join link in context.AliasLinks
                                 on alias.LinkId equals link.AliasLinkId
                                 join client in context.Clients
                                     .AsNoTracking()
                                 on alias.LinkId equals client.AliasLinkId
                                 select client)
                       .Distinct()
                       .Include(c => c.CurrentAlias)
                       .Include(c => c.AliasLink.Children);

                return await iqClients.ToListAsync();
            }
        }

        public async Task<IList<EFClient>> GetRecentClients(int offset, int count)
        {
            using (var context = new DatabaseContext())
                return await context.Clients
                    .AsNoTracking()
                    .Include(c => c.CurrentAlias)
                    .Include(p => p.AliasLink)
                    .OrderByDescending(p => p.ClientId)
                    .Skip(offset)
                    .Take(count)
                    .ToListAsync();
        }

        public async Task<IList<EFClient>> PruneInactivePrivilegedClients(int inactiveDays)
        {
            using (var context = new DatabaseContext())
            {
                var inactive = await context.Clients.Where(c => c.Level > Objects.Player.Permission.Flagged)
                    .AsNoTracking()
                    .Where(c => (DateTime.UtcNow - c.LastConnection).TotalDays >= inactiveDays)
                    .ToListAsync();
                inactive.ForEach(c => c.Level = Player.Permission.User);
                await context.SaveChangesAsync();
                return inactive;
            }
        }

        public async Task<int> GetTotalClientsAsync()
        {
            using (var context = new DatabaseContext())
                return await context.Clients
                    .CountAsync();
        }

        public Task<EFClient> CreateProxy()
        {
            throw new NotImplementedException();
        }

        public async Task<int> GetTotalPlayTime()
        {
            using (var context = new DatabaseContext())
                return await context.Clients.SumAsync(c => c.TotalConnectionTime);
        }
        #endregion
    }
}
