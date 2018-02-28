using SharedLibrary.Database.Models;
using SharedLibrary.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Database
{
    //https://stackoverflow.com/questions/5940225/fastest-way-of-inserting-in-entity-framework
    public static class Importer
    {
        public static void ImportClients(IList<Player> clients)
        {
            DatabaseContext context = null;

            try
            {
                context = new DatabaseContext();
                context.Configuration.AutoDetectChangesEnabled = false;
                context.Configuration.LazyLoadingEnabled = false;
                context.Configuration.ProxyCreationEnabled = false;

                int count = 0;
                foreach (var entityToInsert in clients)
                {
                    ++count;

                    var link = new EFAliasLink() { Active = true };

                    var alias = new EFAlias()
                    {
                        Active = true,
                        DateAdded = entityToInsert.LastConnection,
                        IPAddress = entityToInsert.IPAddress,
                        Link = link,
                        Name = entityToInsert.Name,
                    };

                    var client = new EFClient()
                    {
                        Active = true,
                        AliasLink = link,
                        Connections = entityToInsert.Connections,
                        CurrentAlias = alias,
                        FirstConnection = entityToInsert.LastConnection,
                        Level = entityToInsert.Level,
                        LastConnection = entityToInsert.LastConnection,
                        TotalConnectionTime = entityToInsert.TotalConnectionTime,
                        Masked = entityToInsert.Masked,
                        NetworkId = entityToInsert.NetworkId
                    };

                    context = AddClient(context, client, count, 1000, true);
                }

                context.SaveChanges();
            }
            finally
            {
                if (context != null)
                    context.Dispose();
            }
        }

        private static DatabaseContext AddClient(DatabaseContext context, EFClient client, int count, int commitCount, bool recreateContext)
        {
            context.Clients.Add(client);
            if (count % commitCount == 0)
            {
                try
                {
                    context.SaveChanges();
                }

                catch (Exception)
                {
           
                }

                if (recreateContext)
                {
                    context.Dispose();
                    context = new DatabaseContext();
                    context.Configuration.AutoDetectChangesEnabled = false;
                    context.Configuration.LazyLoadingEnabled = false;
                    context.Configuration.ProxyCreationEnabled = false;
                }
            }

            return context;
        }

        public static void ImportPenalties(IList<Penalty> penalties)
        {
            DatabaseContext context = null;

            try
            {
                context = new DatabaseContext();
                context.Configuration.AutoDetectChangesEnabled = false;
                context.Configuration.LazyLoadingEnabled = false;
                context.Configuration.ProxyCreationEnabled = false;

                int count = 0;
                foreach (var entityToInsert in penalties)
                {
                    ++count;
                    var punisher = entityToInsert.Offender.NetworkId == entityToInsert.Punisher.NetworkId ?
                        context.Clients.SingleOrDefault(c => c.ClientId == 1) :
                        context.Clients.SingleOrDefault(c => c.NetworkId == entityToInsert.Punisher.NetworkId);
                    if (punisher == null)
                        continue;
                    var offender = context.Clients.Include("AliasLink").SingleOrDefault(c => c.NetworkId == entityToInsert.Offender.NetworkId);

                    if (offender == null)
                        continue;

                   
                    var penalty = new EFPenalty()
                    {
                        Active = true,
                        Expires = entityToInsert.Expires.Year == 9999 ? DateTime.Parse(System.Data.SqlTypes.SqlDateTime.MaxValue.ToString()) : entityToInsert.Expires,
                        Offender = offender,
                        Punisher = punisher,
                        Offense = entityToInsert.Offense,
                        Type = entityToInsert.Type,
                        When = entityToInsert.When == DateTime.MinValue ? DateTime.UtcNow : entityToInsert.When,
                        Link = offender.AliasLink
                    };

                    context = AddPenalty(context, penalty, count, 1000, true);
                }

                context.SaveChanges();
            }
            finally
            {
                if (context != null)
                    context.Dispose();
            }
        }

        private static DatabaseContext AddPenalty(DatabaseContext context, EFPenalty penalty, int count, int commitCount, bool recreateContext)
        {
            context.Penalties.Add(penalty);
            if (count % commitCount == 0)
            {
                try
                {
                    context.SaveChanges();
                }

                catch (Exception)
                {

                }

                if (recreateContext)
                {
                    context.Dispose();
                    context = new DatabaseContext();
                    context.Configuration.AutoDetectChangesEnabled = false;
                    context.Configuration.LazyLoadingEnabled = false;
                    context.Configuration.ProxyCreationEnabled = false;
                }
            }

            return context;
        }

        public static void ImportSQLite<T>(IList<T> SQLiteData) where T : class
        {
            DatabaseContext context = null;

            try
            {
                context = new DatabaseContext();
                context.Configuration.AutoDetectChangesEnabled = false;
                context.Configuration.LazyLoadingEnabled = false;
                context.Configuration.ProxyCreationEnabled = false;

                int count = 0;
                foreach (var entityToInsert in SQLiteData)
                {
                    ++count;
                    context = AddSQLite(context, entityToInsert, count, 1000, true);
                }

                context.SaveChanges();
            }
            finally
            {
                if (context != null)
                    context.Dispose();
            }
        }

        private static DatabaseContext AddSQLite<T>(DatabaseContext context, T entity, int count, int commitCount, bool recreateContext) where T : class
        {
            context.Set<T>().Add(entity);

            if (count % commitCount == 0)
            {
                try
                {
                    context.SaveChanges();
                }

                catch (Exception)
                {
                  
                }

                if (recreateContext)
                {
                    context.Dispose();
                    context = new DatabaseContext();
                    context.Configuration.AutoDetectChangesEnabled = false;
                    context.Configuration.LazyLoadingEnabled = false;
                    context.Configuration.ProxyCreationEnabled = false;
                }
            }
            return context;
        }
    }
}

