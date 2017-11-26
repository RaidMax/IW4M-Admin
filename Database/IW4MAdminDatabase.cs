using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Database.Models;

namespace Database
{
    public class IW4MAdminDatabase : SharedLibrary.Interfaces.IDatabase
    {
        private IW4MAdminDatabaseContext _context;

        public IW4MAdminDatabase()
        {
            _context = new IW4MAdminDatabaseContext();
        }

        public SharedLibrary.Interfaces.IDatabaseContext GetContext() => _context;

        public async Task<Client> AddClient(Client newClient)
        {
            var client = _context.Clients.Add(newClient);
            await _context.SaveChangesAsync();
            return client;
        }

        public Client GetClient(int clientID) => _context.Clients.SingleOrDefault(c => c.ClientId == clientID);
        public Client  GetClient(string networkID) => _context.Clients.SingleOrDefault(c => c.NetworkId == networkID);
        public IList<Client> GetOwners() => _context.Clients.Where(c => c.Level == SharedLibrary.Player.Permission.Owner).ToList();
        public IList<SharedLibrary.Player> GetPlayers(IList<string> networkIDs) => _context.Clients.Where(c => networkIDs.Contains(c.NetworkId)).Select(c => c.ToPlayer()).ToList();
        public IList<Penalty> GetPenalties (int clientID) => _context.Penalties.Where(p => p.OffenderId == clientID).ToList();
        public IList<SharedLibrary.Player> GetAdmins() => _context.Clients.Where(c => c.Level > SharedLibrary.Player.Permission.Flagged).Select(c => c.ToPlayer()).ToList();


    }
}
