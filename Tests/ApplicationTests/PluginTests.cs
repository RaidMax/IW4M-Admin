using ApplicationTests.Fixtures;
using ApplicationTests.Mocks;
using FakeItEasy;
using IW4MAdmin;
using IW4MAdmin.Application.Factories;
using IW4MAdmin.Application.Misc;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ApplicationTests
{
    [TestFixture]
    public class PluginTests
    {
        private static string PLUGIN_DIR = @"X:\IW4MAdmin\Plugins\ScriptPlugins";
        private IServiceProvider serviceProvider;
        private IManager fakeManager;
        private EventHandlerMock mockEventHandler;

        [SetUp]
        public void Setup()
        {
            serviceProvider = new ServiceCollection().BuildBase()
                .AddSingleton(A.Fake<ClientService>())
                .AddSingleton<IScriptCommandFactory, ScriptCommandFactory>()
                .AddSingleton(A.Fake<IScriptPluginServiceResolver>())
                .BuildServiceProvider();
            fakeManager = serviceProvider.GetRequiredService<IManager>();
            mockEventHandler = serviceProvider.GetRequiredService<EventHandlerMock>();

            var rconConnectionFactory = serviceProvider.GetRequiredService<IRConConnectionFactory>();

            A.CallTo(() => rconConnectionFactory.CreateConnection(A<string>.Ignored, A<int>.Ignored, A<string>.Ignored))
                 .Returns(serviceProvider.GetRequiredService<IRConConnection>());

            A.CallTo(() => serviceProvider.GetRequiredService<IRConParser>().Configuration)
                .Returns(ConfigurationGenerators.CreateRConParserConfiguration(serviceProvider.GetRequiredService<IParserRegexFactory>()));

            A.CallTo(() => fakeManager.AddEvent(A<GameEvent>.Ignored))
                .Invokes((fakeCall) => mockEventHandler.HandleEvent(fakeManager, fakeCall.Arguments[0] as GameEvent));
        }

        [Test]
        public async Task Test_GenericGuidClientIsKicked()
        {
            var plugin = new ScriptPlugin(serviceProvider.GetRequiredService<ILogger>(), Path.Join(PLUGIN_DIR, "SharedGUIDKick.js"), PLUGIN_DIR);
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            server.GameName = Server.Game.IW4;
            var client = ClientGenerators.CreateBasicClient(server, hasIp: false, clientState: EFClient.ClientState.Connecting);
            client.NetworkId = -1168897558496584395;
            var databaseClient = ClientGenerators.CreateDatabaseClient(hasIp: false);
            databaseClient.NetworkId = client.NetworkId;

            var fakeClientService = serviceProvider.GetRequiredService<ClientService>();
            A.CallTo(() => fakeClientService.GetUnique(A<long>.Ignored))
                .Returns(Task.FromResult(databaseClient));
            A.CallTo(() => fakeManager.GetClientService())
                .Returns(fakeClientService);

            await plugin.Initialize(serviceProvider.GetRequiredService<IManager>(), serviceProvider.GetRequiredService<IScriptCommandFactory>(), serviceProvider.GetRequiredService<IScriptPluginServiceResolver>());

            var gameEvent = new GameEvent()
            {
                Origin = client,
                Owner = server,
                Type = GameEvent.EventType.PreConnect,
                IsBlocking = true
            };

            await server.ExecuteEvent(gameEvent);

            // connect
            var e = mockEventHandler.Events[0];
            await server.ExecuteEvent(e);
            await plugin.OnEventAsync(e, server);

            // kick
            e = mockEventHandler.Events[1];
            await server.ExecuteEvent(e);
        }
    }
}
