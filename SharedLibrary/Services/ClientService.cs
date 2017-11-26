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
        private Dictionary<int, IW4MAdminDatabaseContext> _context;

        public ClientService()
        {
            _context = new Dictionary<int, IW4MAdminDatabaseContext>();
        }
        public async Task<EFClient> Create(EFClient entity)
        {
            using (var context = new IW4MAdminDatabaseContext())
            {

                // get all aliases by IP
                var alias = await context.Aliases.FirstOrDefaultAsync(a => a.IP == entity.IPAddress);
                EFAliasLink link = alias?.Link;

                var client = new EFClient()
                {
                    Active = true,
                    Name = entity.Name,
                    FirstConnection = DateTime.UtcNow,
                    Connections = 1,
                    IPAddress = entity.IPAddress,
                    LastConnection = DateTime.UtcNow,
                    Level = Objects.Player.Permission.User,
                    Masked = false,
                    NetworkId = entity.NetworkId,
                    AliasLink = link ?? new EFAliasLink() { Active = true }
                };

                client.AliasLink.Children.Add(new EFAlias()
                {
                    Active = true,
                    DateAdded = DateTime.UtcNow,
                    IP = entity.IPAddress,
                    Link = client.AliasLink,
                    Name = entity.Name
                });

                context.Clients.Add(client);
                await context.SaveChangesAsync();

                return client;
            }
        }

        public async Task<EFClient> Delete(EFClient entity)
        {
            using (var context = new IW4MAdminDatabaseContext())
            {
                entity = context.Clients.Single(e => e.ClientId == entity.ClientId);
                entity.Active = false;
                entity.Level = Objects.Player.Permission.User;
                context.Entry(entity).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return entity;
            }
        }

        public async Task<IList<EFClient>> Find(Func<EFClient, bool> e)
        {
            using (var context = new IW4MAdminDatabaseContext())
                return await Task.Run(() => context.Clients
                     .Include(c => c.AliasLink.Children)
                     .Where(e).ToList());
        }

        public async Task<EFClient> Get(int entityID)
        {
            using (var context = new IW4MAdminDatabaseContext())
                return await new IW4MAdminDatabaseContext().Clients
                    .Include(c => c.AliasLink.Children)
                    .SingleOrDefaultAsync(e => e.ClientId == entityID);
        }

        public async Task<EFClient> GetUnique(string entityAttribute)
        {
            using (var context = new IW4MAdminDatabaseContext())
            {
                return await context.Clients
                    .Include(c => c.AliasLink.Children)
                    .SingleOrDefaultAsync(c => c.NetworkId == entityAttribute);
            }
        }

        public async Task<EFClient> Update(EFClient entity)
        {
            using (var context = new IW4MAdminDatabaseContext())
            {
                entity = context.Clients.Attach(entity);
                context.Entry(entity).State = EntityState.Modified;
                await context.SaveChangesAsync();
                return entity;
            }
        }

        #region ServiceSpecific
        public async Task<IList<EFClient>> GetOwners()
        {
            using (var context = new IW4MAdminDatabaseContext())
                return await context.Clients.Where(c => c.Level == Objects.Player.Permission.Owner).ToListAsync();
        }

        public async Task<IList<EFClient>> GetPrivilegedClients()
        {
            using (var context = new IW4MAdminDatabaseContext())
                return await new IW4MAdminDatabaseContext().Clients
                .Where(c => c.Level >= Objects.Player.Permission.Trusted)
                .ToListAsync();
        }


        public async Task<IList<EFClient>> GetRecentClients(int offset, int count)
        {
            using (var context = new IW4MAdminDatabaseContext())
                return await context.Clients.OrderByDescending(p => p.ClientId).Skip(offset).Take(count).ToListAsync();
        }

        public async Task<IList<EFClient>> PruneInactivePrivilegedClients(int inactiveDays)
        {
            using (var context = new IW4MAdminDatabaseContext())
            {
                var inactive = await context.Clients.Where(c => c.Level > Objects.Player.Permission.Flagged)
                    .Where(c => (DateTime.UtcNow - c.LastConnection).TotalDays >= inactiveDays)
                    .ToListAsync();
                inactive.ForEach(c => c.Level = Objects.Player.Permission.User);
                await context.SaveChangesAsync();
                return inactive;
            }
        }

        public async Task<int> GetTotalClientsAsync()
        {
            using (var context = new IW4MAdminDatabaseContext())
                return await context.Clients.CountAsync();
        }

        public Task<EFClient> CreateProxy()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
