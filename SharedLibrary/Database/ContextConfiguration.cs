using System;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.SqlServerCompact;
using System.Data.SqlServerCe;

namespace SharedLibrary.Database
{
    class ContextConfiguration : DbConfiguration
    {
        public ContextConfiguration()
        {
            if (!Utilities.IsRunningOnMono())
            {
                SetExecutionStrategy("System.Data.SqlServerCe.4.0", () => new DefaultExecutionStrategy());
                SetProviderFactory("System.Data.SqlServerCe.4.0", new SqlCeProviderFactory());
                SetProviderServices("System.Data.SqlServerCe.4.0", SqlCeProviderServices.Instance);
                SetDefaultConnectionFactory(new SqlCeConnectionFactory("System.Data.SqlServerCe.4.0"));
            }

            else
            {
               /* SetExecutionStrategy("MySql.Data.MySqlClient", () => new DefaultExecutionStrategy());
                SetProviderFactory("MySql.Data.MySqlClient", new MySql.Data.MySqlClient.MySqlClientFactory());
                SetProviderServices("MySql.Data.MySqlClient", new MySql.Data.MySqlClient.MySqlProviderServices());
                SetDefaultConnectionFactory(new MySql.Data.Entity.MySqlConnectionFactory());*/
            }
        }
    }
}
