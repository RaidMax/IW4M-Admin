using IW4MAdmin;
using IW4MAdmin.Application.Misc;
using SharedLibraryCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using FakeItEasy;
using System;
using ApplicationTests.Fixtures;
using SharedLibraryCore.Services;
using SharedLibraryCore.Database.Models;
using System.Threading.Tasks;
using ApplicationTests.Mocks;
using System.Linq;

namespace ApplicationTests
{
    [TestFixture]
    public class IW4MServerTests
    {
        private IServiceProvider serviceProvider;
        private ILogger fakeLogger;
        private IManager fakeManager;
        private IRConConnection fakeRConConnection;
        private IRConParser fakeRConParser;
        private MockEventHandler mockEventHandler;

        [SetUp]
        public void Setup()
        {
            fakeLogger = A.Fake<ILogger>();
            fakeManager = A.Fake<IManager>();
            fakeRConConnection = A.Fake<IRConConnection>();
            var rconConnectionFactory = A.Fake<IRConConnectionFactory>();
            A.CallTo(() => rconConnectionFactory.CreateConnection(A<string>.Ignored, A<int>.Ignored, A<string>.Ignored))
                 .Returns(fakeRConConnection);
            var fakeTranslationLookup = A.Fake<ITranslationLookup>();
            fakeRConParser = A.Fake<IRConParser>();
            A.CallTo(() => fakeRConParser.Configuration)
                .Returns(ConfigurationGenerators.CreateRConParserConfiguration(A.Fake<IParserRegexFactory>()));

            mockEventHandler = new MockEventHandler();
            A.CallTo(() => fakeManager.GetEventHandler())
                .Returns(mockEventHandler);

            serviceProvider = new ServiceCollection()
                .AddSingleton(new IW4MServer(fakeManager, ConfigurationGenerators.CreateServerConfiguration(), fakeTranslationLookup, rconConnectionFactory)
                {
                    RconParser = fakeRConParser
                })
                .BuildServiceProvider();
        }

        [Test]
        public void Test_GenerateLogPath_Basic()
        {
            string expected = "C:\\Game\\main\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BasePathDirectory = "C:\\Game",
                GameDirectory = "main",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_WithMod()
        {
            string expected = "C:\\Game\\mods\\mod\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BasePathDirectory = "C:\\Game",
                GameDirectory = "main",
                ModDirectory = "mods\\mod",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_WithBaseGame()
        {
            string expected = "C:\\GameAlt\\main\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BaseGameDirectory = "C:\\GameAlt",
                BasePathDirectory = "C:\\Game",
                GameDirectory = "main",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_WithBaseGameAndMod()
        {
            string expected = "C:\\GameAlt\\mods\\mod\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BaseGameDirectory = "C:\\GameAlt",
                BasePathDirectory = "C:\\Game",
                GameDirectory = "main",
                ModDirectory = "mods\\mod",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_InvalidBasePath()
        {
            string expected = "C:\\Game\\main\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BaseGameDirectory = "game",
                BasePathDirectory = "C:\\Game",
                GameDirectory = "main",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_BadSeparators()
        {
            string expected = "C:\\Game\\main\\folder\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BasePathDirectory = "C:/Game",
                GameDirectory = "main/folder",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_RelativeBasePath()
        {
            string expected = "C:\\Game\\main\\folder\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BaseGameDirectory = "main\\folder",
                BasePathDirectory = "C:\\Game",
                GameDirectory = "main\\folder",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_FixWineDriveMangling()
        {
            string expected = "/opt/server/game/log.log";
            var info = new LogPathGeneratorInfo()
            {
                BasePathDirectory = "Z:\\opt\\server",
                GameDirectory = "game",
                LogFile = "log.log",
                IsWindows = false
            };
            string generated = IW4MServer.GenerateLogPath(info).Replace('\\', '/');

            Assert.AreEqual(expected, generated);
        }

        #region BAN
        [Test]
        public async Task Test_BanCreatesPenalty()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);

            await server.Ban("test reason", target, origin);

            A.CallTo(() => fakePenaltyService.Create(A<EFPenalty>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task Test_BanExecutesKickCommand()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);

            await server.Ban("test reason", target, origin);

            A.CallTo(() => fakeRConParser.ExecuteCommandAsync(fakeRConConnection, "kick"))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task Test_BanQueuesSetLevelEvent()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);

            await server.Ban("test reason", target, origin);

            Assert.IsTrue(mockEventHandler.Events.Any(_event => _event.Type == SharedLibraryCore.GameEvent.EventType.ChangePermission &&
            _event.Origin == origin &&
            _event.Target == target &&
            (EFClient.Permission)_event.Extra == EFClient.Permission.Banned));
        }

        [Test]
        public async Task Test_BanFindsIngameClientToExecuteFor()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var origin = ClientGenerators.CreateBasicClient(server);
            var target = ClientGenerators.CreateBasicClient(server, isIngame: false);
            var ingameTarget = ClientGenerators.CreateBasicClient(server);

            A.CallTo(() => fakeManager.GetActiveClients())
                .Returns(new[] { ingameTarget });

            await server.Ban("test reason", target, origin);

            Assert.IsTrue(mockEventHandler.Events.Any(_event => _event.Target == ingameTarget));
        }
        #endregion

        #region TEMPBAN
        [Test]
        public async Task Test_TempBanCreatesPenalty()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);

            await server.TempBan("test reason", TimeSpan.Zero, target, origin);

            A.CallTo(() => fakePenaltyService.Create(A<EFPenalty>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task Test_TempBanExecutesKickCommand()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);

            await server.TempBan("test reason", TimeSpan.Zero, target, origin);

            A.CallTo(() => fakeRConParser.ExecuteCommandAsync(fakeRConConnection, "kick"))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task Test_TempBanFindsIngameClientToExecuteFor()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var origin = ClientGenerators.CreateBasicClient(server);
            var target = ClientGenerators.CreateBasicClient(server, isIngame: false);

            var ingameTarget = ClientGenerators.CreateBasicClient(server);

            A.CallTo(() => fakeManager.GetActiveClients())
                .Returns(new[] { ingameTarget });

            await server.TempBan("test reason", TimeSpan.Zero, target, origin);

            A.CallTo(() => fakeRConParser.ExecuteCommandAsync(fakeRConConnection, "kick"))
                .MustHaveHappenedOnceExactly();
        }
        #endregion

        #region KICK
        [Test]
        public async Task Test_KickCreatesPenalty()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);

            await server.Kick("test reason", target, origin);

            A.CallTo(() => fakePenaltyService.Create(A<EFPenalty>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task Test_KickExecutesKickCommand()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);

            await server.Kick("test reason", target, origin);

            A.CallTo(() => fakeRConParser.ExecuteCommandAsync(fakeRConConnection, "kick"))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task Test_KickQueuesPredisconnectEvent()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);

            await server.Kick("test reason", target, origin);

            Assert.IsTrue(mockEventHandler.Events.Any(_event => _event.Type == SharedLibraryCore.GameEvent.EventType.PreDisconnect && _event.Origin == target));
        }

        [Test]
        public async Task Test_KickFindsIngameClientToExecuteFor()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var origin = ClientGenerators.CreateBasicClient(server);
            var target = ClientGenerators.CreateBasicClient(server, isIngame: false);

            var ingameTarget = ClientGenerators.CreateBasicClient(server);

            A.CallTo(() => fakeManager.GetActiveClients())
                .Returns(new[] { ingameTarget });

            await server.Kick("test reason", target, origin);

            // kick creates a pre disconnect event
            Assert.IsTrue(mockEventHandler.Events.Any(_event => _event.Origin == ingameTarget));
        }
        #endregion

        #region WARN
        [Test]
        public async Task Test_WarnCreatesPenalty()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);

            await server.Warn("test reason", target, origin);

            A.CallTo(() => fakePenaltyService.Create(A<EFPenalty>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        [Test]
        public async Task Test_WarnBroadCastMessageForIngameClient()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);

            await server.Warn("test reason", target, origin);

            Assert.IsTrue(mockEventHandler.Events.Any(_event => _event.Type == SharedLibraryCore.GameEvent.EventType.Broadcast));
        }

        [Test]
        public async Task Test_WarnLimitReachedQueuesKickEvent()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);
            target.Warnings = 5;

            await server.Warn("test reason", target, origin);

            Assert.IsTrue(mockEventHandler.Events.Any(_event => _event.Type == SharedLibraryCore.GameEvent.EventType.Kick && _event.Target == target));
        }
        #endregion

        #region UNBAN
        [Test]
        public async Task Test_UnbanQueuesSetLevelEvent()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);
            A.CallTo(() => fakePenaltyService.RemoveActivePenalties(A<int>.Ignored))
                .Returns(Task.CompletedTask);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var origin = ClientGenerators.CreateBasicClient(server);
            var target = ClientGenerators.CreateBasicClient(server);

            target.Level = EFClient.Permission.Banned;
            target.AliasLink = new EFAliasLink();

            await server.Unban("test reason", target, origin);

            Assert.IsTrue(mockEventHandler.Events.Any(_event => _event.Type == SharedLibraryCore.GameEvent.EventType.ChangePermission && _event.Target == target));
        }

        [Test]
        public async Task Test_UnbanRemovedActivePenalties()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);
            A.CallTo(() => fakePenaltyService.RemoveActivePenalties(A<int>.Ignored))
                .Returns(Task.CompletedTask);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var origin = ClientGenerators.CreateBasicClient(server);
            var target = ClientGenerators.CreateBasicClient(server);

            target.Level = EFClient.Permission.Banned;
            target.AliasLink = new EFAliasLink()
            {
                AliasLinkId = 1
            };

            await server.Unban("test reason", target, origin);

            A.CallTo(() => fakePenaltyService.RemoveActivePenalties(target.AliasLink.AliasLinkId))
                .MustHaveHappened();
        }

        [Test]
        public async Task Test_UnbanCreatesPenalty()
        {
            var fakePenaltyService = A.Fake<PenaltyService>();
            A.CallTo(() => fakeManager.GetPenaltyService())
                .Returns(fakePenaltyService);
            A.CallTo(() => fakePenaltyService.RemoveActivePenalties(A<int>.Ignored))
                .Returns(Task.CompletedTask);

            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var origin = ClientGenerators.CreateBasicClient(server);
            var target = ClientGenerators.CreateBasicClient(server);

            target.Level = EFClient.Permission.Banned;
            target.AliasLink = new EFAliasLink()
            {
                AliasLinkId = 1
            };

            await server.Unban("test reason", target, origin);

            A.CallTo(() => fakePenaltyService.Create(A<EFPenalty>.Ignored))
                .MustHaveHappened();
        }
        #endregion
    }
}
