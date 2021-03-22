using System;
using System.Collections.Generic;

namespace Data.Extensions
{
    public static class MigrationExtensions
    {
        public static bool IsMigration => Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Migration";
    }
}