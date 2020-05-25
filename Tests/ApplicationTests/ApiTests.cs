using ApplicationTests.Fixtures;
using FakeItEasy;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SharedLibraryCore.Dtos;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;
using Stats.Dtos;
using StatsWeb.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WebfrontCore.Controllers.API;
using WebfrontCore.Controllers.API.Dtos;
using WebfrontCore.Controllers.API.Validation;

namespace ApplicationTests
{
    [TestFixture]
    public class ApiTests
    {
        private IServiceProvider serviceProvider;
        private IDatabaseContextFactory contextFactory;
        private ClientController clientController;
        private StatsWeb.API.StatsController statsController;
        private IResourceQueryHelper<FindClientRequest, FindClientResult> fakeClientQueryHelper;
        private IResourceQueryHelper<StatsInfoRequest, StatsInfoResult> fakeStatsQueryHelper;


        [SetUp]
        public void Setup()
        {
            var collection = new ServiceCollection();

            collection.AddMvc()
                .AddFluentValidation();

            serviceProvider = collection.AddSingleton<ClientController>()
                .AddSingleton<StatsWeb.API.StatsController>()
               .AddSingleton(A.Fake<IResourceQueryHelper<FindClientRequest, FindClientResult>>())
               .AddSingleton(A.Fake<IResourceQueryHelper<StatsInfoRequest, StatsInfoResult>>())
               .AddTransient<IValidator<FindClientRequest>, FindClientRequestValidator>()
               .BuildBase()
               .BuildServiceProvider();

            clientController = serviceProvider.GetRequiredService<ClientController>();
            statsController = serviceProvider.GetRequiredService<StatsWeb.API.StatsController>();
            contextFactory = serviceProvider.GetRequiredService<IDatabaseContextFactory>();
            fakeClientQueryHelper = serviceProvider.GetRequiredService<IResourceQueryHelper<FindClientRequest, FindClientResult>>();
            fakeStatsQueryHelper = serviceProvider.GetRequiredService<IResourceQueryHelper<StatsInfoRequest, StatsInfoResult>>();
        }

        #region CLIENT_CONTROLLER
        [Test]
        public async Task Test_ClientController_FindAsync_Happy()
        {
            var query = new FindClientRequest()
            {
                Name = "test"
            };

            int expectedClientId = 123;

            A.CallTo(() => fakeClientQueryHelper.QueryResource(A<FindClientRequest>.Ignored))
                .Returns(Task.FromResult(new ResourceQueryHelperResult<FindClientResult>()
                {
                    Results = new[]
                    {
                        new FindClientResult()
                        {
                            ClientId = expectedClientId
                        }
                    }
                }));

            var result = await clientController.FindAsync(query);
            Assert.IsInstanceOf<OkObjectResult>(result);

            var viewResult = (result as OkObjectResult).Value as FindClientResponse;
            Assert.NotNull(viewResult);
            Assert.AreEqual(expectedClientId, viewResult.Clients.First().ClientId);
        }

        [Test]
        public async Task Test_ClientController_FindAsync_InvalidModelState()
        {
            var query = new FindClientRequest();

            clientController.ModelState.AddModelError("test", "test");
            var result = await clientController.FindAsync(query);
            Assert.IsInstanceOf<BadRequestObjectResult>(result);
            clientController.ModelState.Clear();
        }

        [Test]
        public async Task Test_ClientController_FindAsync_Exception()
        {
            string expectedExceptionMessage = "failure";
            int expectedStatusCode = 500;
            var query = new FindClientRequest();
            A.CallTo(() => fakeClientQueryHelper.QueryResource(A<FindClientRequest>.Ignored))
                .Throws(new Exception(expectedExceptionMessage));

            var result = await clientController.FindAsync(query);
            Assert.IsInstanceOf<ObjectResult>(result);

            var statusResult = (result as ObjectResult);
            Assert.AreEqual(expectedStatusCode, statusResult.StatusCode);
            //Assert.IsTrue((statusResult.Value as ErrorResponse).Messages.Contains(expectedExceptionMessage));
        }
        #endregion

        #region STATS_CONTROLLER
        [Test]
        public async Task Test_StatsController_ClientStats_Happy()
        {
            var client = ClientGenerators.CreateBasicClient(null);
          
            var query = new StatsInfoRequest
            {
                ClientId = client.ClientId
            };

            var queryResult = new ResourceQueryHelperResult<StatsInfoResult>()
            {
                Results = new[]
                {
                    new StatsInfoResult
                    {
                        Deaths = 1,
                        Kills = 1,
                        LastPlayed = DateTime.Now,
                        Performance = 100,
                        Ranking = 10,
                        ScorePerMinute = 500,
                        ServerGame = "IW4",
                        ServerId = 123,
                        ServerName = "IW4Host",
                        TotalSecondsPlayed = 100
                    }
                },
                TotalResultCount = 1,
                RetrievedResultCount = 1
            };

            A.CallTo(() => fakeStatsQueryHelper.QueryResource(A<StatsInfoRequest>.Ignored))
                .Returns(Task.FromResult(queryResult));

            var result = await statsController.ClientStats(query.ClientId.Value);
            Assert.IsInstanceOf<OkObjectResult>(result);

            var viewResult = (result as OkObjectResult).Value as IEnumerable<StatsInfoResult>;
            Assert.NotNull(viewResult);
            Assert.AreEqual(queryResult.Results, viewResult);
        }

        [Test]
        public async Task Test_StatsController_ClientStats_InvalidModelState()
        {
            statsController.ModelState.AddModelError("test", "test");
            var result = await statsController.ClientStats(1);
            Assert.IsInstanceOf<BadRequestObjectResult>(result);
            statsController.ModelState.Clear();
        }

        [Test]
        public async Task Test_StatsController_ClientStats_Exception()
        {
            string expectedExceptionMessage = "failure";
            int expectedStatusCode = 500;

            A.CallTo(() => fakeStatsQueryHelper.QueryResource(A<StatsInfoRequest>.Ignored))
               .Throws(new Exception(expectedExceptionMessage));

            var result = await statsController.ClientStats(1);
            Assert.IsInstanceOf<ObjectResult>(result);

            var statusResult = (result as ObjectResult);
            Assert.AreEqual(expectedStatusCode, statusResult.StatusCode);
            Assert.IsTrue((statusResult.Value as ErrorResponse).Messages.Contains(expectedExceptionMessage));
        }

        [Test]
        public async Task Test_StatsController_ClientStats_NotFound()
        {
            var queryResult = new ResourceQueryHelperResult<StatsInfoResult>()
            {
                Results = new List<StatsInfoResult>()
            };

            A.CallTo(() => fakeStatsQueryHelper.QueryResource(A<StatsInfoRequest>.Ignored))
                .Returns(Task.FromResult(queryResult));

            var result = await statsController.ClientStats(1);
            Assert.IsInstanceOf<NotFoundResult>(result);
        }
        #endregion
    }
}
