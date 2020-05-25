using ApplicationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationTests
{
    [TestFixture]
    public class ServiceTests
    {
        private IServiceProvider serviceProvider;
        private IDatabaseContextFactory contextFactory;
        private ClientService clientService;

        [SetUp]
        public void Setup()
        {
            serviceProvider = new ServiceCollection()
                .AddSingleton<ClientService>()
                .BuildBase()

                .BuildServiceProvider();

            contextFactory = serviceProvider.GetRequiredService<IDatabaseContextFactory>();
            clientService = serviceProvider.GetRequiredService<ClientService>();
        }

        #region QUERY_RESOURCE
        [Test]
        public async Task Test_QueryClientResource_Xuid()
        {
            var client = ClientGenerators.CreateBasicClient(null);
            client.NetworkId = -1;

            var query = new FindClientRequest()
            {
                Xuid = client.NetworkId.ToString("X")
            };

            using var context = contextFactory.CreateContext();
            
            context.Clients.Add(client);
            await context.SaveChangesAsync();

            var result = await clientService.QueryResource(query);

            Assert.IsNotEmpty(result.Results);
            Assert.AreEqual(query.Xuid, result.Results.First().Xuid);

            context.Clients.Remove(client);
            await context.SaveChangesAsync();
        }

        [Test]
        public async Task Test_QueryClientResource_NameExactMatch()
        {
            var query = new FindClientRequest()
            {
                Name = "test"
            };

            using var context = contextFactory.CreateContext();
            var client = ClientGenerators.CreateBasicClient(null);
            client.Name = query.Name;
            context.Clients.Add(client);
            await context.SaveChangesAsync();

            var result = await clientService.QueryResource(query);

            Assert.IsNotEmpty(result.Results);
            Assert.AreEqual(query.Name, result.Results.First().Name);

            context.Clients.Remove(client);
            await context.SaveChangesAsync();
        }

        [Test]
        public async Task Test_QueryClientResource_NameCaseInsensitivePartial()
        {
            var query = new FindClientRequest()
            {
                Name = "TEST"
            };

            using var context = contextFactory.CreateContext();
            var client = ClientGenerators.CreateBasicClient(null);
            client.Name = "atesticle";
            context.Clients.Add(client);
            await context.SaveChangesAsync();

            var result = await clientService.QueryResource(query);

            Assert.IsNotEmpty(result.Results);
            Assert.IsTrue(result.Results.First().Name.ToUpper().Contains(query.Name));

            context.Clients.Remove(client);
            await context.SaveChangesAsync();
        }

        [Test]
        public async Task Test_QueryClientResource_SortDirection()
        {
            var firstClient = ClientGenerators.CreateBasicClient(null);
            firstClient.ClientId = 0;
            firstClient.NetworkId = -1;
            firstClient.LastConnection = DateTime.Now.AddHours(-1);
            firstClient.Name = "test";
            var secondClient = ClientGenerators.CreateBasicClient(null);
            secondClient.ClientId = 0;
            secondClient.NetworkId = -2;
            secondClient.LastConnection = DateTime.Now;
            secondClient.Name = firstClient.Name;

            var query = new FindClientRequest()
            {
                Name = firstClient.Name
            };

            using var context = contextFactory.CreateContext();

            context.Clients.Add(firstClient);
            context.Clients.Add(secondClient);
            await context.SaveChangesAsync();

            var result = await clientService.QueryResource(query);

            Assert.IsNotEmpty(result.Results);
            Assert.AreEqual(secondClient.NetworkId.ToString("X"), result.Results.First().Xuid);
            Assert.AreEqual(firstClient.NetworkId.ToString("X"), result.Results.Last().Xuid);

            query.Direction = SortDirection.Ascending;
            result = await clientService.QueryResource(query);

            Assert.IsNotEmpty(result.Results);
            Assert.AreEqual(firstClient.NetworkId.ToString("X"), result.Results.First().Xuid);
            Assert.AreEqual(secondClient.NetworkId.ToString("X"), result.Results.Last().Xuid);

            context.Clients.Remove(firstClient);
            context.Clients.Remove(secondClient);
            await context.SaveChangesAsync();
        }

        [Test]
        public async Task Test_QueryClientResource_NoMatch()
        {
            var query = new FindClientRequest()
            {
                Name = "test"
            };

            using var context = contextFactory.CreateContext();
            var client = ClientGenerators.CreateBasicClient(null);
            client.Name = "client";
            context.Clients.Add(client);
            await context.SaveChangesAsync();

            var result = await clientService.QueryResource(query);

            Assert.IsEmpty(result.Results);

            context.Clients.Remove(client);
            await context.SaveChangesAsync();
        }
        #endregion
    }
}
