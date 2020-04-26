using Microsoft.EntityFrameworkCore;
using SharedLibraryCore.Database;
using SharedLibraryCore.Interfaces;
using System;

namespace ApplicationTests.Mocks
{
    class DatabaseContextFactoryMock : IDatabaseContextFactory
    {
        private DatabaseContext ctx;
        private readonly IServiceProvider _serviceProvider;

        public DatabaseContextFactoryMock(IServiceProvider sp)
        {
            _serviceProvider = sp;
        }

        public DatabaseContext CreateContext(bool? enableTracking)
        {
            if (ctx == null)
            {
                var contextOptions = new DbContextOptionsBuilder<DatabaseContext>()
                    .UseInMemoryDatabase(databaseName: "database")
                    .Options;

                ctx = new DatabaseContext(contextOptions);
            }

            return ctx;
        }
    }
}
