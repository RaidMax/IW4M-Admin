using NUnit.Framework;
using System;
using SharedLibraryCore.Interfaces;
using IW4MAdmin;
using FakeItEasy;
using System.Linq;
using SharedLibraryCore.Database.Models;
using Microsoft.Extensions.DependencyInjection;
using ApplicationTests.Fixtures;
using System.Threading.Tasks;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore;
using ApplicationTests.Mocks;
using SharedLibraryCore.Services;
using static SharedLibraryCore.Database.Models.EFClient;

namespace ApplicationTests
{
    [TestFixture]
    public class CommandTests
    {
        ILogger logger;
        private IServiceProvider serviceProvider;
        private ITranslationLookup transLookup;
        private CommandConfiguration cmdConfig;
        private ApplicationConfiguration appConfig;
        private EventHandlerMock mockEventHandler;
        private ClientService clientService;
        private IManager manager;

        [SetUp]
        public void Setup()
        {
            logger = A.Fake<ILogger>();
            cmdConfig = new CommandConfiguration();

            serviceProvider = new ServiceCollection()
                .BuildBase(new EventHandlerMock(true))
                .AddSingleton(A.Fake<ClientService>())
                .BuildServiceProvider()
                .SetupTestHooks();

            mockEventHandler = serviceProvider.GetRequiredService<EventHandlerMock>();
            manager = serviceProvider.GetRequiredService<IManager>();
            transLookup = serviceProvider.GetRequiredService<ITranslationLookup>();
            clientService = serviceProvider.GetRequiredService<ClientService>();
            appConfig = serviceProvider.GetRequiredService<ApplicationConfiguration>();

            A.CallTo(() => manager.GetClientService())
                .Returns(clientService);

            A.CallTo(() => clientService.UpdateLevel(A<Permission>.Ignored, A<EFClient>.Ignored, A<EFClient>.Ignored))
                .Returns(Task.CompletedTask);

            A.CallTo(() => manager.GetCommands())
                .Returns(new Command[]
                {
                    new ImpersonatableCommand(cmdConfig, transLookup),
                    new NonImpersonatableCommand(cmdConfig, transLookup),
                    new MockCommand(cmdConfig, transLookup)
                });

            A.CallTo(() => manager.AddEvent(A<GameEvent>.Ignored))
               .Invokes((fakeCall) => mockEventHandler.HandleEvent(manager, fakeCall.Arguments[0] as GameEvent));
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

        #region SETLEVEL
        [Test]
        public async Task Test_SetLevelFailOnSelf()
        {
            var cmd = new SetLevelCommand(cmdConfig, transLookup, logger);
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = Permission.Owner;

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = target,
                Data = "Administrator",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.Owner, target.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission));
        }

        [Test]
        public async Task Test_SetLevelFailWithSourcePrivilegeTooLow()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var cmd = new SetLevelCommand(cmdConfig, transLookup, logger);
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.Level = Permission.Moderator;
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = Permission.Administrator;

            A.CallTo(() => clientService.GetOwnerCount())
                .Returns(Task.FromResult(1));

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Data = "Administrator",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.Administrator, target.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission));
        }

        [Test]
        public async Task Test_SetLevelFailWithExistingOwner_AndOnlyOneOwnerAllowed()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var cmd = new SetLevelCommand(cmdConfig, transLookup, logger);
            var origin = ClientGenerators.CreateBasicClient(server);
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = Permission.User;

            A.CallTo(() => clientService.GetOwnerCount())
                .Returns(Task.FromResult(1));

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Data = "Owner",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.User, target.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission));
        }

        [Test]
        public async Task Test_SetLevelFailWithStepPrivilegesDisabled_AndNonOwner()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var cmd = new SetLevelCommand(cmdConfig, transLookup, logger);
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.Level = Permission.SeniorAdmin;
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = Permission.Moderator;

            A.CallTo(() => clientService.GetOwnerCount())
                .Returns(Task.FromResult(1));

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Data = "Administrator",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.Moderator, target.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission));
        }

        [Test]
        public async Task Test_SetLevelFailWithStepPrivilegesEnabled_ButNewPermissionTooHigh()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var cmd = new SetLevelCommand(cmdConfig, transLookup, logger);
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.Level = Permission.Moderator;
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = Permission.User;
            appConfig.EnableSteppedHierarchy = true;

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Data = "Moderator",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.User, target.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission));
        }

        [Test]
        public async Task Test_SetLevelFailInvalidGroup()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var cmd = new SetLevelCommand(cmdConfig, transLookup, logger);
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.Level = Permission.Owner;
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = Permission.User;

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Data = "Banned",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.User, target.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission));
        }

        [Test]
        public async Task Test_SetLevelSucceedWithNoExistingOwner_AndOnlyOneOwnerAllowed()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var cmd = new SetLevelCommand(cmdConfig, transLookup, logger);
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.Level = Permission.Owner;
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = Permission.User;

            A.CallTo(() => clientService.GetOwnerCount())
                .Returns(Task.FromResult(0));

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Data = "Owner",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.Owner, target.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission && !_event.Failed));
        }

        [Test]
        public async Task Test_SetLevelOwnerSucceedWithMultiOwnerAllowed()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var cmd = new SetLevelCommand(cmdConfig, transLookup, logger);
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.Level = Permission.Owner;
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = Permission.User;
            appConfig.EnableMultipleOwners = true;

            A.CallTo(() => clientService.GetOwnerCount())
                .Returns(Task.FromResult(1));

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Data = "Owner",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.Owner, target.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission && !_event.Failed));
        }

        [Test]
        public async Task Test_SetLevelOwnerSucceedWithMultiOwnerAllowed_AndSteppedPrivileges()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var cmd = new SetLevelCommand(cmdConfig, transLookup, logger);
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.Level = Permission.Owner;
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = Permission.User;
            appConfig.EnableMultipleOwners = true;
            appConfig.EnableSteppedHierarchy = true;

            A.CallTo(() => clientService.GetOwnerCount())
                .Returns(Task.FromResult(1));

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Data = "Owner",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.Owner, target.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission && !_event.Failed));
        }

        [Test]
        public async Task Test_SetLevelSucceedWithSteppedPrivileges()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var cmd = new SetLevelCommand(cmdConfig, transLookup, logger);
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.Level = Permission.Moderator;
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = Permission.User;
            appConfig.EnableSteppedHierarchy = true;

            A.CallTo(() => clientService.GetOwnerCount())
                .Returns(Task.FromResult(1));

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Data = "Trusted",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.Trusted, target.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission && !_event.Failed));
        }

        [Test]
        public async Task Test_SetLevelSucceed()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var cmd = new SetLevelCommand(cmdConfig, transLookup, logger);
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.Level = Permission.Owner;
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = Permission.User;
            appConfig.EnableSteppedHierarchy = true;

            A.CallTo(() => clientService.GetOwnerCount())
                .Returns(Task.FromResult(1));

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Data = "Trusted",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.Trusted, target.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission && !_event.Failed));
        }

        [Test]
        public async Task Test_SetLevelSucceed_AndFindsIngameClient()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var cmd = new SetLevelCommand(cmdConfig, transLookup, logger);
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.Level = Permission.Owner;
            var databaseTarget = ClientGenerators.CreateDatabaseClient();
            databaseTarget.Level = Permission.Administrator;

            var ingameTarget = ClientGenerators.CreateBasicClient(server);
            ingameTarget.Level = Permission.Administrator;

            A.CallTo(() => manager.GetActiveClients())
                .Returns(new[] { ingameTarget });

            A.CallTo(() => clientService.GetOwnerCount())
                .Returns(Task.FromResult(1));

            var gameEvent = new GameEvent()
            {
                Target = databaseTarget,
                Origin = origin,
                Data = "User",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.User, ingameTarget.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission && !_event.Failed));
        }
        #endregion

        #region PREFIX_PROCESSING
        [Test]
        public async Task Test_CommandProcessing_IsBroadcastCommand()
        {
            string broadcastPrefix = "@@";
            var config = ConfigurationGenerators.CreateApplicationConfiguration();
            config.BroadcastCommandPrefix = broadcastPrefix;
            var server = serviceProvider.GetRequiredService<IW4MServer>();

            var cmd = EventGenerators.GenerateEvent(GameEvent.EventType.Command, $"{broadcastPrefix}{nameof(MockCommand)}", server);

            var result = await CommandProcessing.ValidateCommand(cmd, config);
            Assert.AreEqual(nameof(MockCommand), result.Name);
            Assert.IsTrue(result.IsBroadcast);
        }
        #endregion

        #region PMADMINS
        [Test]
        public async Task Test_PrivateMessageAdmins_HappyPath()
        {
            var cmd = new PrivateMessageAdminsCommand(cmdConfig, transLookup);
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var origin = ClientGenerators.CreateDatabaseClient();
            origin.Level = Permission.Administrator;
            origin.CurrentServer = server;
            var gameEvent = EventGenerators.GenerateEvent(GameEvent.EventType.Command, "", server);
            cmdConfig.Commands.Add(nameof(PrivateMessageAdminsCommand), new CommandProperties { SupportedGames = new[] { server.GameName } });
            
            server.Clients[0] = origin;
            server.Clients[1] = origin;
            await cmd.ExecuteAsync(gameEvent);
            int expectedEvents = 2;

            Assert.AreEqual(expectedEvents, mockEventHandler.Events.Count(_event => _event.Type == GameEvent.EventType.Tell));
        }

        [Test]
        public async Task Test_PrivateMessageAdmins_GameNotSupported()
        {
            var cmd = new PrivateMessageAdminsCommand(cmdConfig, transLookup);
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var origin = ClientGenerators.CreateDatabaseClient();
            origin.Level = Permission.Administrator;
            origin.CurrentServer = server;
            var gameEvent = EventGenerators.GenerateEvent(GameEvent.EventType.Command, "", server);
            gameEvent.Origin = origin;
            cmdConfig.Commands.Add(nameof(PrivateMessageAdminsCommand), new CommandProperties());

            server.Clients[0] = origin;
            server.Clients[1] = origin;
            await cmd.ExecuteAsync(gameEvent);
            int expectedEvents = 1;

            Assert.AreEqual(expectedEvents, mockEventHandler.Events.Count(_event => _event.Type == GameEvent.EventType.Tell));
        }
        #endregion
    }
}
