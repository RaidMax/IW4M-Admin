using ApplicationTests.Fixtures;
using ApplicationTests.Mocks;
using FakeItEasy;
using IW4MAdmin;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WebfrontCore.Controllers;

namespace ApplicationTests
{
    public class ControllerTests
    {
        private IServiceProvider serviceProvider;
        private IDatabaseContextFactory contextFactory;
        private IW4MServer server;
        private IManager manager;

        [SetUp]
        public void Setup()
        {
            serviceProvider = new ServiceCollection()
                .BuildBase()
                .AddSingleton<ActionController>()
                .AddSingleton<IManagerCommand, BanCommand>()
                .AddSingleton<IManagerCommand, TempBanCommand>()
                .AddSingleton<IManagerCommand, UnbanCommand>()
                .AddSingleton<IManagerCommand, KickCommand>()
                .AddSingleton<IManagerCommand, FlagClientCommand>()
                .AddSingleton<IManagerCommand, UnflagClientCommand>()
                .AddSingleton<IManagerCommand, SayCommand>()
                .BuildServiceProvider()
                .SetupTestHooks();

            contextFactory = serviceProvider.GetRequiredService<IDatabaseContextFactory>();
            server = serviceProvider.GetRequiredService<IW4MServer>();
            manager = serviceProvider.GetRequiredService<IManager>();
            A.CallTo(() => manager.GetServers())
                .Returns(new[] { server });
            A.CallTo(() => manager.GetActiveClients())
                .Returns(new[] { ClientGenerators.CreateBasicClient(server) });
        }

        #region ACTION_CONTROLLER
        [Test]
        public async Task Test_BanCommand_Redirects_WithCommandText()
        {
            var controller = serviceProvider.GetRequiredService<ActionController>();
            var expectedCommandText = "!ban @1 test";
            var expectedEndpoint = server.EndPoint;

            var result = await controller.BanAsync(1, "test", 6) as RedirectToActionResult;

            Assert.AreEqual(expectedEndpoint, result.RouteValues["serverId"]);
            Assert.AreEqual(expectedCommandText, result.RouteValues["command"]);
        }

        [Test]
        public async Task Test_UnbanCommand_Redirects_WithCommandText()
        {
            var controller = serviceProvider.GetRequiredService<ActionController>();
            var expectedCommandText = "!unban @1 test";
            var expectedEndpoint = server.EndPoint;

            var result = await controller.UnbanAsync(1, "test") as RedirectToActionResult;

            Assert.AreEqual(expectedEndpoint, result.RouteValues["serverId"]);
            Assert.AreEqual(expectedCommandText, result.RouteValues["command"]);
        }

        [Test]
        public async Task Test_Say_Redirects_WithCommandText()
        {
            var controller = serviceProvider.GetRequiredService<ActionController>();
            var expectedCommandText = "!say test";
            var expectedEndpoint = server.EndPoint;

            var result = await controller.ChatAsync(expectedEndpoint, "test") as RedirectToActionResult;

            Assert.AreEqual(expectedEndpoint, result.RouteValues["serverId"]);
            Assert.AreEqual(expectedCommandText, result.RouteValues["command"]);
        }

        [Test]
        public async Task Test_Kick_Redirects_WithCommandText()
        {
            var controller = serviceProvider.GetRequiredService<ActionController>();
            var expectedCommandText = "!kick 0 test";
            var expectedEndpoint = server.EndPoint;

            var result = await controller.KickAsync(1, "test") as RedirectToActionResult;

            Assert.AreEqual(expectedEndpoint, result.RouteValues["serverId"]);
            Assert.AreEqual(expectedCommandText, result.RouteValues["command"]);
        }

        [Test]
        public async Task Test_Flag_Redirects_WithCommandText()
        {
            var controller = serviceProvider.GetRequiredService<ActionController>();
            var expectedCommandText = "!flag @1 test";
            var expectedEndpoint = server.EndPoint;

            var result = await controller.FlagAsync(1, "test") as RedirectToActionResult;

            Assert.AreEqual(expectedEndpoint, result.RouteValues["serverId"]);
            Assert.AreEqual(expectedCommandText, result.RouteValues["command"]);
        }

        [Test]
        public async Task Test_Unflag_Redirects_WithCommandText()
        {
            var controller = serviceProvider.GetRequiredService<ActionController>();
            var expectedCommandText = "!unflag @1 test";
            var expectedEndpoint = server.EndPoint;

            var result = await controller.UnflagAsync(1, "test") as RedirectToActionResult;

            Assert.AreEqual(expectedEndpoint, result.RouteValues["serverId"]);
            Assert.AreEqual(expectedCommandText, result.RouteValues["command"]);
        }

        [Test]
        public async Task Test_TempBan_Redirects_WithCommandText()
        {
            var controller = serviceProvider.GetRequiredService<ActionController>();
            var expectedCommandText = "!tempban @1 1G test"; // 'G' because no localization is loaded (GLOBAL_WEEKS)
            var expectedEndpoint = server.EndPoint;

            var result = await controller.BanAsync(1, "test", 5) as RedirectToActionResult;

            Assert.AreEqual(expectedEndpoint, result.RouteValues["serverId"]);
            Assert.AreEqual(expectedCommandText, result.RouteValues["command"]);
        }
        #endregion
    }
}
