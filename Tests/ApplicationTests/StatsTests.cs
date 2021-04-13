using NUnit.Framework;
using System;
using SharedLibraryCore.Interfaces;
using IW4MAdmin;
using FakeItEasy;
using IW4MAdmin.Application.EventParsers;
using System.Linq;
using IW4MAdmin.Plugins.Stats.Models;
using IW4MAdmin.Plugins.Stats.Config;
using System.Collections.Generic;
using SharedLibraryCore.Database.Models;
using Microsoft.Extensions.DependencyInjection;
using IW4MAdmin.Plugins.Stats.Helpers;
using ApplicationTests.Fixtures;
using System.Threading.Tasks;
using Stats.Helpers;
using Stats.Dtos;
using SharedLibraryCore.Configuration;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ApplicationTests
{
    [TestFixture]
    public class StatsTests
    {
        ILogger logger;
        private IServiceProvider serviceProvider;
        private IConfigurationHandlerFactory handlerFactory;
        private IDatabaseContextFactory contextFactory;

        [SetUp]
        public void Setup()
        {
            logger = A.Fake<ILogger>();
            handlerFactory = A.Fake<IConfigurationHandlerFactory>();

            serviceProvider = new ServiceCollection()
                .BuildBase()
                .AddSingleton(A.Fake<IConfigurationHandler<StatsConfiguration>>())
                .AddSingleton<StatsResourceQueryHelper>()
                .AddSingleton(new ServerConfiguration() { IPAddress = "127.0.0.1", Port = 28960 })
                .AddSingleton<StatManager>()
                .AddSingleton<IW4MAdmin.Plugins.Stats.Plugin>()
                .BuildServiceProvider();

            contextFactory = serviceProvider.GetRequiredService<IDatabaseContextFactory>();
        }

        [Test]
        public void TestKDR()
        {
            var mgr = A.Fake<IManager>();
            var config = A.Fake<IConfigurationHandler<StatsConfiguration>>();
            var plugin = serviceProvider.GetRequiredService<IW4MAdmin.Plugins.Stats.Plugin>();

            A.CallTo(() => config.Configuration())
                .Returns(new StatsConfiguration()
                {
                    EnableAntiCheat = true
                });

            A.CallTo(() => handlerFactory.GetConfigurationHandler<StatsConfiguration>(A<string>.Ignored))
                .Returns(config);

            var server = serviceProvider.GetRequiredService<IW4MServer>();

            var parser = new BaseEventParser(A.Fake<IParserRegexFactory>(), A.Fake<ILogger>(), A.Fake<ApplicationConfiguration>());
            parser.Configuration.GuidNumberStyle = System.Globalization.NumberStyles.Integer;

            var log = System.IO.File.ReadAllLines("Files\\T6GameStats.log");
            plugin.OnLoadAsync(mgr).Wait();
            plugin.OnEventAsync(new SharedLibraryCore.GameEvent() { Type = SharedLibraryCore.GameEvent.EventType.Start, Owner = server }, server).Wait();

            var clientList = new Dictionary<long, EFClient>();

            foreach (string line in log)
            {
                var e = parser.GenerateGameEvent(line);
                if (e.Origin != null)
                {
                    //if (!clientList.ContainsKey(e.Origin.NetworkId))
                    //{
                    //    clientList.Add(e.Origin.NetworkId, e.Origin);
                    //}

                    //else
                    //{
                    //    e.Origin = clientList[e.Origin.NetworkId];
                    //}

                    e.Origin = server.GetClientsAsList().FirstOrDefault(_client => _client.NetworkId == e.Origin.NetworkId) ?? e.Origin;
                    e.Origin.CurrentServer = server;
                }

                if (e.Target != null)
                {
                    //if (!clientList.ContainsKey(e.Target.NetworkId))
                    //{
                    //    clientList.Add(e.Target.NetworkId, e.Target);
                    //}

                    //else
                    //{
                    //    e.Target = clientList[e.Target.NetworkId];
                    //}

                    e.Target = server.GetClientsAsList().FirstOrDefault(_client => _client.NetworkId == e.Target.NetworkId) ?? e.Target;
                    e.Target.CurrentServer = server;
                }

                server.ExecuteEvent(e).Wait();
                plugin.OnEventAsync(e, server).Wait();
            }

            var client = server.GetClientsAsList().First(_client => _client?.NetworkId == 2028755667);
            var stats = client.GetAdditionalProperty<EFClientStatistics>("ClientStats");
        }

        class BasePathProvider : IBasePathProvider
        {
            public string BasePath => @"X:\IW4MAdmin\BUILD\Plugins";
        }

        [Test]
        public async Task Test_ConcurrentCallsToUpdateStatHistoryDoesNotCauseException()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var configHandler = serviceProvider.GetRequiredService<IConfigurationHandler<StatsConfiguration>>();
            var mgr = serviceProvider.GetRequiredService<StatManager>();
            var target = ClientGenerators.CreateDatabaseClient();
            target.CurrentServer = server;

            A.CallTo(() => configHandler.Configuration())
                .Returns(new StatsConfiguration()
                {
                    TopPlayersMinPlayTime = 0
                });

            var dbFactory = serviceProvider.GetRequiredService<IDatabaseContextFactory>();
            var db = dbFactory.CreateContext(true);
            var efServer = new EFServer()
            {
                EndPoint = server.EndPoint.ToString()
            };
            db.Set<EFServer>().Add(efServer);

            db.Clients.Add(target);
            db.SaveChanges();

            mgr.AddServer(server);
            await mgr.AddPlayer(target);
            var stats = target.GetAdditionalProperty<EFClientStatistics>("ClientStats");

            await mgr.UpdateStatHistory(target, stats);

            db.Clients.Remove(target);
            db.Set<EFServer>().Remove(efServer);
            await db.SaveChangesAsync();
        }

        #region QUERY_HELPER
        [Test]
        public async Task Test_StatsQueryHelper_Get()
        {
            var queryHelper = serviceProvider.GetRequiredService<StatsResourceQueryHelper>();
            await using var context = contextFactory.CreateContext();

            var server = new EFServer() { ServerId = 1 };
            var stats = new EFClientStatistics()
            {
                Client = ClientGenerators.CreateBasicClient(null),
                SPM = 100,
                Server = server
            };

            var ratingHistory = new EFClientRatingHistory()
            {
                Client = stats.Client,
                Ratings = new[]
                {
                    new EFRating()
                    {
                        Ranking = 100,
                        Server = server,
                        Newest = true
                    }
                }
            };

            context.Set<EFClientStatistics>().Add(stats);
            context.Set<EFClientRatingHistory>().Add(ratingHistory);
            await context.SaveChangesAsync();

            var query = new StatsInfoRequest()
            {
                ClientId = stats.Client.ClientId
            };
            var result = await queryHelper.QueryResource(query);

            Assert.IsNotEmpty(result.Results);
            Assert.AreEqual(stats.SPM, result.Results.First().ScorePerMinute);
            Assert.AreEqual(ratingHistory.Ratings.First().Ranking, result.Results.First().Ranking);

            context.Set<EFClientStatistics>().Remove(stats);
            context.Set<EFClientRatingHistory>().Remove(ratingHistory);
            context.Set<EFServer>().Remove(server);
            await context.SaveChangesAsync();
        }
        #endregion
    }
}
