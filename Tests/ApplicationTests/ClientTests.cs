using ApplicationTests.Fixtures;
using FakeItEasy;
using IW4MAdmin;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using System;
using System.Collections.Generic;
using System.Text;
using SharedLibraryCore.Configuration;

namespace ApplicationTests
{
    [TestFixture]
    public class ClientTests
    {
        private IServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            serviceProvider = new ServiceCollection()
                .BuildBase()
                .BuildServiceProvider();
        }

        #region KICK
        [Test]
        public void Test_Kick_Happy()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);
            origin.Level = EFClient.Permission.Owner;

            var result = target.Kick("test", origin);

            Assert.False(result.Failed);
            Assert.AreEqual(EFClient.ClientState.Disconnecting, target.State);
        }

        [Test]
        public void Test_Kick_FailSamePermission()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);

            var result = target.Kick("test", origin);

            Assert.True(result.Failed);
            Assert.AreEqual(GameEvent.EventFailReason.Permission, result.FailReason);
        }

        [Test]
        public void Test_Kick_FailLessPermission()
        {
            var server = serviceProvider.GetRequiredService<IW4MServer>();
            var target = ClientGenerators.CreateBasicClient(server);
            var origin = ClientGenerators.CreateBasicClient(server);
            target.Level = EFClient.Permission.Owner;

            var result = target.Kick("test", origin);

            Assert.True(result.Failed);
            Assert.AreEqual(GameEvent.EventFailReason.Permission, result.FailReason);
        }
        #endregion
    }
}
