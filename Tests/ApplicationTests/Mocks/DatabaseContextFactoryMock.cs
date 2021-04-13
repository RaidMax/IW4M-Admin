using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Interfaces;
using System;
using SharedLibraryCore.Database.MigrationContext;

namespace ApplicationTests.Mocks
{
    class DatabaseContextFactoryMock : IDatabaseContextFactory
    {
        public DatabaseContext CreateContext(bool? enableTracking)
        {
            var contextOptions = new DbContextOptionsBuilder<SqliteDatabaseContext>()
                .UseInMemoryDatabase(databaseName: "database")
                .Options;

            return new SqliteDatabaseContext(contextOptions);
        }
    }
}