using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.Entity;

using SharedLibrary.Database;
using SharedLibrary.Database.Models;
using System.Linq.Expressions;

namespace SharedLibrary.Services
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
                var existingAlias = aliases.SingleOrDefault(a => a.Name == entity.Name);
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
                    Level = hasExistingAlias ?
                        context.Clients.First(c => c.CurrentAliasId == existingAlias.AliasId).Level :
                        Objects.Player.Permission.User,
                    FirstConnection = DateTime.UtcNow,
                    Connections = 1,
                    LastConnection = DateTime.UtcNow,
                    Masked = false,
                    NetworkId = entity.NetworkId,
                    AliasLink = aliasLink,
                    CurrentAlias = existingAlias
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
            using (var context = new DatabaseContext())
                return await Task.Run(() => context.Clients
                      .AsNoTracking()
                     .Include(c => c.CurrentAlias)
                     .Include(c => c.AliasLink.Children)
                     .Where(e).ToList());
        }

        public async Task<EFClient> Get(int entityID)
        {
            using (var context = new DatabaseContext())
                return await new DatabaseContext().Clients
                    .AsNoTracking()
                    .Include(c => c.CurrentAlias)
                    .Include(c => c.AliasLink.Children)
                    .SingleOrDefaultAsync(e => e.ClientId == entityID);
        }

        public async Task<EFClient> GetUnique(string entityAttribute)
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
                    .Include(c=> c.CurrentAlias)
                    .Single(e => e.ClientId == entity.ClientId);

                // if their level has been changed
                if (entity.Level != client.Level)
                {
                    // get all clients that use the same aliasId
                    var matchingClients = await context.Clients
                        .Where(c => c.CurrentAliasId == client.CurrentAliasId)
                        .ToListAsync();

                    // update all related clients level
                    matchingClients.ForEach(c => c.Level = (client.Level == Objects.Player.Permission.Banned) ?
                        client.Level : entity.Level);
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

                // set remaining non-navigation properties that may have been updated
                client.Level = entity.Level;
                client.LastConnection = entity.LastConnection;
                client.Connections = entity.Connections;
                client.FirstConnection = entity.FirstConnection;
                client.Masked = entity.Masked;
                client.TotalConnectionTime = entity.TotalConnectionTime;

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
                    .Where(c => c.Level == Objects.Player.Permission.Owner)
                    .ToListAsync();
        }

        public async Task<IList<EFClient>> GetPrivilegedClients()
        {
            using (var context = new DatabaseContext())
                return await new DatabaseContext().Clients
                    .AsNoTracking()
                    .Include(c => c.CurrentAlias)
                    .Where(c => c.Level >= Objects.Player.Permission.Trusted)
                    .ToListAsync();
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
                inactive.ForEach(c => c.Level = Objects.Player.Permission.User);
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
#endregion
    }
}
