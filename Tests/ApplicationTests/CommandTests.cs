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
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore;
using ApplicationTests.Mocks;

namespace ApplicationTests
{
    [TestFixture]
    public class CommandTests
    {
        ILogger logger;
        private IServiceProvider serviceProvider;
        private ITranslationLookup transLookup;
        private CommandConfiguration cmdConfig;
        private MockEventHandler mockEventHandler;

        [SetUp]
        public void Setup()
        {
            logger = A.Fake<ILogger>();
            cmdConfig = new CommandConfiguration();

            serviceProvider = new ServiceCollection()
                .BuildBase()
                .BuildServiceProvider();

            mockEventHandler = new MockEventHandler(true);
            A.CallTo(() => serviceProvider.GetRequiredService<IManager>().GetEventHandler())
                .Returns(mockEventHandler);

            var mgr = serviceProvider.GetRequiredService<IManager>();
            transLookup = serviceProvider.GetRequiredService<ITranslationLookup>();

            A.CallTo(() => mgr.GetCommands())
                .Returns(new Command[]
                {
                    new ImpersonatableCommand(cmdConfig, transLookup),
                    new NonImpersonatableCommand(cmdConfig, transLookup)
                });

            //Utilities.DefaultCommandTimeout = new TimeSpan(0, 0, 2);
        }

        #region RUNAS
        [Test]
        public async Task Test_RunAsFailsOnSelf()
        {
            var cmd = new RunAsCommand(cmdConfig, transLookup);
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = target
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Command));
        }

        [Test]
        public async Task Test_RunAsFailsOnHigherPrivilege()
        {
            var cmd = new RunAsCommand(cmdConfig, transLookup);
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = EFClient.Permission.Administrator;
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.NetworkId = 100;
            origin.Level = EFClient.Permission.Moderator;

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Command));
        }

        [Test]
        public async Task Test_RunAsFailsOnSamePrivilege()
        {
            var cmd = new RunAsCommand(cmdConfig, transLookup);
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = EFClient.Permission.Administrator;
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.NetworkId = 100;
            origin.Level = EFClient.Permission.Administrator;

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Command));
        }

        [Test]
        public async Task Test_RunAsFailsOnDisallowedCommand()
        {
            var cmd = new RunAsCommand(cmdConfig, transLookup);
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = EFClient.Permission.Moderator;
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.NetworkId = 100;
            origin.Level = EFClient.Permission.Administrator;

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Owner = server,
                Data = nameof(NonImpersonatableCommand)
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            // failed when validating the command
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Command && _event.FailReason == GameEvent.EventFailReason.Invalid));
        }

        [Test]
        public async Task Test_RunAsQueuesEventAndResponse()
        {
            var cmd = new RunAsCommand(cmdConfig, transLookup);
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = EFClient.Permission.Moderator;
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.NetworkId = 100;
            origin.Level = EFClient.Permission.Administrator;

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Data = nameof(ImpersonatableCommand),
                Owner = server
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell /*&& _event.Target == origin todo: fake the command result*/ ));
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Command && !_event.Failed));
        }
        #endregion
    }
}
