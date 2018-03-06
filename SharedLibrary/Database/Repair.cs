using SharedLibrary.Interfaces;
using System;
using System.Data.SqlServerCe;

namespace SharedLibrary.Database
{
    public class Repair
    {
        public static void Run(ILogger log)
        {
            SqlCeEngine engine = new SqlCeEngine(@"Data Source=|DataDirectory|\Database.sdf");
            if (false == engine.Verify())
            {
                log.WriteWarning("Database is corrupted.");
                try
                {
                    engine.Repair(null, RepairOption.DeleteCorruptedRows);
                }
                catch (SqlCeException ex)
                {
                    log.WriteError(ex.Message);
                }
            }
        }
    }
}