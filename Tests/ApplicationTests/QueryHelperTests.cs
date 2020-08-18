using ApplicationTests.Fixtures;
using IW4MAdmin.Application.Meta;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.QueryHelper;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ApplicationTests
{
    public class QueryHelperTests
    {
        private IServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            serviceProvider = new ServiceCollection().BuildBase()
                .AddSingleton<AdministeredPenaltyResourceQueryHelper>()
                .AddSingleton<ReceivedPenaltyResourceQueryHelper>()
                .AddSingleton<UpdatedAliasResourceQueryHelper>()
                .BuildServiceProvider();

            SetupPenalties();
            SetupAliases();
        }

        private void SetupAliases()
        {
            using var ctx = serviceProvider.GetRequiredService<IDatabaseContextFactory>().CreateContext();

            var client = ClientGenerators.CreateDatabaseClient();

            var aliases = new[]
            {
                new EFAlias()
                {
                    LinkId = client.AliasLinkId,
                    Name = "Test1",
                    IPAddress = -1,
                    DateAdded = DateTime.UtcNow.AddMinutes(-1)
                },
                new EFAlias()
                {
                    LinkId = client.AliasLinkId,
                    Name = "Test2",
                    IPAddress = -1,
                    DateAdded = DateTime.UtcNow
                }
            };

            ctx.Aliases.AddRange(aliases);
            ctx.SaveChanges();
        }

        private void SetupPenalties()
        {
            using var ctx = serviceProvider.GetRequiredService<IDatabaseContextFactory>().CreateContext();

            var firstPenalty = PenaltyGenerators.Create(occurs: DateTime.UtcNow.AddMinutes(-2), reason: "first");
            var secondPenalty = PenaltyGenerators.Create(occurs: DateTime.UtcNow.AddMinutes(-1), reason: "second", originClient: firstPenalty.Punisher, targetClient: firstPenalty.Offender);
            var linkedPenalty = PenaltyGenerators.Create(occurs: DateTime.UtcNow, reason: "linked", originClient: firstPenalty.Punisher, targetClient: ClientGenerators.CreateDatabaseClient(clientId: 3));

            ctx.Add(firstPenalty);
            ctx.Add(secondPenalty);
            ctx.Add(linkedPenalty);
            ctx.SaveChanges();
        }

        [TearDown]
        public void Teardown()
        {
            using var ctx = serviceProvider.GetRequiredService<IDatabaseContextFactory>().CreateContext();
            ctx.Database.EnsureDeleted();
        }

        #region ADMINISTERED PENALTIES
        [Test]
        public async Task Test_AdministeredPenaltyResourceQueryHelper_QueryResource_TakesAppropriateCount()
        {
            var queryHelper = serviceProvider.GetRequiredService<AdministeredPenaltyResourceQueryHelper>();

            var request = new ClientPaginationRequest
            {
                Count = 1,
                Before = DateTime.UtcNow,
                ClientId = 1
            };

            var result = await queryHelper.QueryResource(request);

            Assert.AreEqual(request.Count, result.RetrievedResultCount);
        }

        [Test]
        public async Task Test_AdministeredPenaltyResourceQueryHelper_QueryResource_OrdersDescending()
        {
            var queryHelper = serviceProvider.GetRequiredService<AdministeredPenaltyResourceQueryHelper>();

            var request = new ClientPaginationRequest
            {
                Count = 2,
                Before = DateTime.UtcNow,
                ClientId = 1,
                Direction = SortDirection.Descending
            };

            var result = await queryHelper.QueryResource(request);

            Assert.Less(result.Results.Last().When.ToFileTimeUtc(), result.Results.First().When.ToFileTimeUtc());
        }
        #endregion

        #region RECEIVED PENALTIES
        [Test]
        public async Task Test_ReceivedPenaltyResourceQueryHelper_QueryResource_TakesAppropriateCount()
        {
            var queryHelper = serviceProvider.GetRequiredService<ReceivedPenaltyResourceQueryHelper>();

            var request = new ClientPaginationRequest
            {
                Count = 1,
                Before = DateTime.UtcNow,
                ClientId = 2
            };

            var result = await queryHelper.QueryResource(request);

            Assert.AreEqual(request.Count, result.RetrievedResultCount);
        }

        [Test]
        public async Task Test_ReceivedPenaltyResourceQueryHelper_QueryResource_OrdersDescending()
        {
            var queryHelper = serviceProvider.GetRequiredService<ReceivedPenaltyResourceQueryHelper>();

            var request = new ClientPaginationRequest
            {
                Count = 2,
                Before = DateTime.UtcNow,
                ClientId = 2,
                Direction = SortDirection.Descending
            };

            var result = await queryHelper.QueryResource(request);

            Assert.Less(result.Results.Last().When.ToFileTimeUtc(), result.Results.First().When.ToFileTimeUtc());
        }

        [Test]
        public async Task Test_ReceivedPenaltyResourceQueryHelper_QueryResource_IncludesLinkedPenalty()
        {
            var queryHelper = serviceProvider.GetRequiredService<ReceivedPenaltyResourceQueryHelper>();

            var request = new ClientPaginationRequest
            {
                Count = 3,
                Before = DateTime.UtcNow,
                ClientId = 3,
            };

            var result = await queryHelper.QueryResource(request);

            Assert.AreEqual(request.Count, result.RetrievedResultCount);
        }
        #endregion

        #region ALIAS UPDATE
        [Test]
        public async Task Test_UpdatedAliasResourceQueryHelper_QueryResource_TakesAppropriateCount()
        {
            var queryHelper = serviceProvider.GetRequiredService<UpdatedAliasResourceQueryHelper>();

            var request = new ClientPaginationRequest
            {
                Count = 1,
                Before = DateTime.UtcNow,
                ClientId = 1
            };

            var result = await queryHelper.QueryResource(request);

            Assert.AreEqual(request.Count, result.RetrievedResultCount);
        }

        [Test]
        public async Task Test_UpdatedAliasResourceQueryHelper_QueryResource_OrdersDescending()
        {
            var queryHelper = serviceProvider.GetRequiredService<UpdatedAliasResourceQueryHelper>();

            var request = new ClientPaginationRequest
            {
                Count = 2,
                Before = DateTime.UtcNow,
                ClientId = 1,
                Direction = SortDirection.Descending
            };

            var result = await queryHelper.QueryResource(request);

            Assert.Less(result.Results.Last().When.ToFileTimeUtc(), result.Results.First().When.ToFileTimeUtc());
        }
        #endregion
    }
}
