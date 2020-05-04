using NUnit.Framework;
using System;
using SharedLibraryCore.Interfaces;
using IW4MAdmin;
using FakeItEasy;
using IW4MAdmin.Application.EventParsers;
using System.Linq;
using IW4MAdmin.Plugins.Stats.Models;
using IW4MAdmin.Application.Helpers;
using IW4MAdmin.Plugins.Stats.Config;
using System.Collections.Generic;
using SharedLibraryCore.Database.Models;
using Microsoft.Extensions.DependencyInjection;
using IW4MAdmin.Plugins.Stats.Helpers;
using ApplicationTests.Fixtures;
using System.Threading.Tasks;

namespace ApplicationTests
{
    [TestFixture]
    public class StatsTests
    {
        ILogger logger;
        private IServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            logger = A.Fake<ILogger>();

            serviceProvider = new ServiceCollection()
                .BuildBase()
                .BuildServiceProvider();

            void testLog(string msg) => Console.WriteLine(msg);

            A.CallTo(() => logger.WriteError(A<string>.Ignored)).Invokes((string msg) => testLog(msg));
            A.CallTo(() => logger.WriteWarning(A<string>.Ignored)).Invokes((string msg) => testLog(msg));
            A.CallTo(() => logger.WriteInfo(A<string>.Ignored)).Invokes((string msg) => testLog(msg));
            A.CallTo(() => logger.WriteDebug(A<string>.Ignored)).Invokes((string msg) => testLog(msg));
        }

        [Test]
        public void TestKDR()
        {
            var mgr = A.Fake<IManager>();
            var handlerFactory = A.Fake<IConfigurationHandlerFactory>();
            var config = A.Fake<IConfigurationHandler<StatsConfiguration>>();
            var plugin = new IW4MAdmin.Plugins.Stats.Plugin(handlerFactory, null);

            A.CallTo(() => config.Configuration())
                .Returns(new StatsConfiguration()
                {
                    EnableAntiCheat = true
                });

            A.CallTo(() => handlerFactory.GetConfigurationHandler<StatsConfiguration>(A<string>.Ignored))
                .Returns(config);

            A.CallTo(() => mgr.GetLogger(A<long>.Ignored))
                .Returns(logger);

            var server = new IW4MServer(mgr,
                new SharedLibraryCore.Configuration.ServerConfiguration() { IPAddress = "127.0.0.1", Port = 28960 },
                A.Fake<ITranslationLookup>(),
                A.Fake<IRConConnectionFactory>(), A.Fake<IGameLogReaderFactory>());

            var parser = new BaseEventParser(A.Fake<IParserRegexFactory>(), A.Fake<ILogger>());
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
            var configHandler = A.Fake<IConfigurationHandler<StatsConfiguration>>();
            var mgr = new StatManager(serviceProvider.GetRequiredService<IManager>(), serviceProvider.GetRequiredService<IDatabaseContextFactory>(), configHandler);
            var target = ClientGenerators.CreateDatabaseClient();
            target.CurrentServer = server;

            A.CallTo(() => configHandler.Configuration())
                .Returns(new StatsConfiguration()
                {
                    TopPlayersMinPlayTime = 0
                });

            var dbFactory = serviceProvider.GetRequiredService<IDatabaseContextFactory>();
            var db = dbFactory.CreateContext(true);
            db.Set<EFServer>().Add(new EFServer()
            {
                EndPoint = server.EndPoint.ToString()
            });

            db.Clients.Add(target);
            db.SaveChanges();

            mgr.AddServer(server);
            await mgr.AddPlayer(target);
            var stats = target.GetAdditionalProperty<EFClientStatistics>("ClientStats");

            await mgr.UpdateStatHistory(target, stats);
        }
    }
}
