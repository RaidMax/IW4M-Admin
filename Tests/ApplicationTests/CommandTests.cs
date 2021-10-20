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
using FluentAssertions;
using FluentAssertions.Extensions;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ApplicationTests
{
    [TestFixture]
    public class CommandTests
    {
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
            serviceProvider = new ServiceCollection()
                .BuildBase(new EventHandlerMock(true))
                .AddSingleton(A.Fake<ClientService>())
                .AddSingleton<LoadMapCommand>()
                .AddSingleton<SetLevelCommand>()
                .AddSingleton<RunAsCommand>()
                .AddSingleton<PrivateMessageAdminsCommand>()
                .AddSingleton<KickCommand>()
                .AddSingleton<WarnCommand>()
                .AddSingleton<TempBanCommand>()
                .AddSingleton<BanCommand>()
                .BuildServiceProvider()
                .SetupTestHooks();

            mockEventHandler = serviceProvider.GetRequiredService<EventHandlerMock>();
            manager = serviceProvider.GetRequiredService<IManager>();
            transLookup = serviceProvider.GetRequiredService<ITranslationLookup>();
            clientService = serviceProvider.GetRequiredService<ClientService>();
            appConfig = serviceProvider.GetRequiredService<ApplicationConfiguration>();
            appConfig.MapChangeDelaySeconds = 1;
            cmdConfig = serviceProvider.GetRequiredService<CommandConfiguration>();
            serviceProvider.GetService<IW4MServer>().RconParser =
                serviceProvider.GetService<IRConParser>();

            Utilities.DefaultLogger = serviceProvider.GetRequiredService<ILogger>();

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
            var cmd = serviceProvider.GetRequiredService<RunAsCommand>();
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
            var cmd = serviceProvider.GetRequiredService<RunAsCommand>();
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
            var cmd = serviceProvider.GetRequiredService<RunAsCommand>();
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
            var cmd = serviceProvider.GetRequiredService<RunAsCommand>();
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
            var cmd = serviceProvider.GetRequiredService<RunAsCommand>();
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
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
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
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
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
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
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
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
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
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
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
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
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
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
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
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
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
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
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
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
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
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
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
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
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
        
        [Test]
        public async Task Test_SetLevelFail_WhenFlagged()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var cmd = serviceProvider.GetRequiredService<SetLevelCommand>();
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.Level = Permission.Owner;
            var target = ClientGenerators.CreateBasicClient(server);
            target.Level = Permission.Flagged;

            var gameEvent = new GameEvent()
            {
                Target = target,
                Origin = origin,
                Data = "Banned",
                Owner = server,
            };

            await cmd.ExecuteAsync(gameEvent);

            Assert.AreEqual(Permission.Flagged, target.Level);
            Assert.IsNotEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.Tell));
            Assert.IsEmpty(mockEventHandler.Events.Where(_event => _event.Type == GameEvent.EventType.ChangePermission));
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
            var cmd = serviceProvider.GetRequiredService<PrivateMessageAdminsCommand>();
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
            var cmd = serviceProvider.GetRequiredService<PrivateMessageAdminsCommand>();
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

        #region LOADMAP
        [Test]
        public void Test_LoadMap_WaitsAppropriateTime_BeforeExecutingCommand()
        {
            var cmd = serviceProvider.GetRequiredService<LoadMapCommand>();
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var rconParser = serviceProvider.GetRequiredService<IRConParser>();
            server.Maps.Add(new Map()
            {
                Name = "mp_test",
                Alias = "test"
            });
            var gameEvent = EventGenerators.GenerateEvent(GameEvent.EventType.Command, server.Maps.First().Name, server);

            Func<Task> act = () => cmd.ExecuteAsync(gameEvent);

            act.ExecutionTime().Should().BeCloseTo(appConfig.MapChangeDelaySeconds.Seconds(), 500.Milliseconds());
            A.CallTo(() => rconParser.ExecuteCommandAsync(A<IRConConnection>.Ignored, A<string>.Ignored))
                .MustHaveHappened();
        }

        [Test]
        public async Task Test_LoadMap_FindsMapName_FromPartialAlias()
        {
            var cmd = serviceProvider.GetRequiredService<LoadMapCommand>();
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var rconParser = serviceProvider.GetRequiredService<IRConParser>();
            server.Maps.Add(new Map()
            {
                Name = "mp_test",
                Alias = "test"
            });
            var gameEvent = EventGenerators.GenerateEvent(GameEvent.EventType.Command, server.Maps.First().Name, server);

            await cmd.ExecuteAsync(gameEvent);

            A.CallTo(() => rconParser.ExecuteCommandAsync(A<IRConConnection>.Ignored, A<string>.That.Contains(server.Maps[0].Name)))
                .MustHaveHappened();
        }
        #endregion
        
        #region REASON_FROM_RULE
        [Test]
        public async Task Test_Warn_WithGlobalRule()
        {
            var expectedReason = "testglobalrule";
            appConfig.GlobalRules = new[] {expectedReason};
            var command = serviceProvider.GetRequiredService<WarnCommand>();
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var gameEvent = EventGenerators.GenerateEvent(GameEvent.EventType.Command, "rule1", server);
            gameEvent.Origin.CurrentServer = server;
            gameEvent.Target = gameEvent.Origin;

            await command.ExecuteAsync(gameEvent);
            
            Assert.NotNull(mockEventHandler.Events
                .FirstOrDefault(e => e.Data == expectedReason && 
                                     e.Type == GameEvent.EventType.Warn));
        }
        
        [Test]
        public async Task Test_Warn_WithServerRule()
        {          
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var expectedReason = "testserverrule";
            appConfig.Servers = new [] { new ServerConfiguration()
            {
                IPAddress = server.IP,
                Port = server.Port,
                Rules = new []{ expectedReason }
            }};
            var command = serviceProvider.GetRequiredService<WarnCommand>();
            var gameEvent = EventGenerators.GenerateEvent(GameEvent.EventType.Command, "serverrule1", server);
            gameEvent.Origin.CurrentServer = server;
            gameEvent.Target = gameEvent.Origin;

            await command.ExecuteAsync(gameEvent);
            
            Assert.NotNull(mockEventHandler.Events
                .FirstOrDefault(e => e.Data == expectedReason && 
                                     e.Type == GameEvent.EventType.Warn));
        }
        
        [Test]
        public async Task Test_Kick_WithGlobalRule()
        {
            var expectedReason = "testglobalrule";
            appConfig.GlobalRules = new[] {expectedReason};
            var command = serviceProvider.GetRequiredService<KickCommand>();
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var gameEvent = EventGenerators.GenerateEvent(GameEvent.EventType.Command, "rule1", server);
            gameEvent.Origin.CurrentServer = server;
            gameEvent.Target = gameEvent.Origin;

            await command.ExecuteAsync(gameEvent);
            
            Assert.NotNull(mockEventHandler.Events
                .FirstOrDefault(e => e.Data == expectedReason && 
                                     e.Type == GameEvent.EventType.Kick));
        }
        
        [Test]
        public async Task Test_Kick_WithServerRule()
        {          
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var expectedReason = "testserverrule";
            appConfig.Servers = new [] { new ServerConfiguration()
            {
                IPAddress = server.IP,
                Port = server.Port,
                Rules = new []{ expectedReason }
            }};
            var command = serviceProvider.GetRequiredService<KickCommand>();
            var gameEvent = EventGenerators.GenerateEvent(GameEvent.EventType.Command, "serverrule1", server);
            gameEvent.Origin.CurrentServer = server;
            gameEvent.Target = gameEvent.Origin;

            await command.ExecuteAsync(gameEvent);
            
            Assert.NotNull(mockEventHandler.Events
                .FirstOrDefault(e => e.Data == expectedReason && 
                                     e.Type == GameEvent.EventType.Kick));
        }
        
        [Test]
        public async Task Test_TempBan_WithGlobalRule()
        {
            var expectedReason = "testglobalrule";
            appConfig.GlobalRules = new[] {expectedReason};
            var command = serviceProvider.GetRequiredService<TempBanCommand>();
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var gameEvent = EventGenerators.GenerateEvent(GameEvent.EventType.Command, "1h rule1", server);
            gameEvent.Origin.CurrentServer = server;
            gameEvent.Target = gameEvent.Origin;

            await command.ExecuteAsync(gameEvent);
            
            Assert.NotNull(mockEventHandler.Events
                .FirstOrDefault(e => e.Data == expectedReason && 
                                     e.Type == GameEvent.EventType.TempBan));
        }
        
        [Test]
        public async Task Test_TempBan_WithServerRule()
        {          
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var expectedReason = "testserverrule";
            appConfig.Servers = new [] { new ServerConfiguration()
            {
                IPAddress = server.IP,
                Port = server.Port,
                Rules = new []{ expectedReason }
            }};
            var command = serviceProvider.GetRequiredService<TempBanCommand>();
            var gameEvent = EventGenerators.GenerateEvent(GameEvent.EventType.Command, "1h serverrule1", server);
            gameEvent.Origin.CurrentServer = server;
            gameEvent.Target = gameEvent.Origin;

            await command.ExecuteAsync(gameEvent);
            
            Assert.NotNull(mockEventHandler.Events
                .FirstOrDefault(e => e.Data == expectedReason && 
                                     e.Type == GameEvent.EventType.TempBan));
        }
        
        [Test]
        public async Task Test_Ban_WithGlobalRule()
        {
            var expectedReason = "testglobalrule";
            appConfig.GlobalRules = new[] {expectedReason};
            var command = serviceProvider.GetRequiredService<BanCommand>();
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var gameEvent = EventGenerators.GenerateEvent(GameEvent.EventType.Command, "rule1", server);
            gameEvent.Origin.CurrentServer = server;
            gameEvent.Target = gameEvent.Origin;

            await command.ExecuteAsync(gameEvent);
            
            Assert.NotNull(mockEventHandler.Events
                .FirstOrDefault(e => e.Data == expectedReason && 
                                     e.Type == GameEvent.EventType.Ban));
        }
        
        [Test]
        public async Task Test_Ban_WithServerRule()
        {          
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var expectedReason = "testserverrule";
            appConfig.Servers = new [] { new ServerConfiguration()
            {
                IPAddress = server.IP,
                Port = server.Port,
                Rules = new []{ expectedReason }
            }};
            var command = serviceProvider.GetRequiredService<BanCommand>();
            var gameEvent = EventGenerators.GenerateEvent(GameEvent.EventType.Command, "serverrule1", server);
            gameEvent.Origin.CurrentServer = server;
            gameEvent.Target = gameEvent.Origin;

            await command.ExecuteAsync(gameEvent);
            
            Assert.NotNull(mockEventHandler.Events
                .FirstOrDefault(e => e.Data == expectedReason && 
                                     e.Type == GameEvent.EventType.Ban));
        }
        #endregion
    }
}
