using IW4MAdmin.Application;
using SharedLibraryCore;
using SharedLibraryCore.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Xunit;

namespace Tests
{
    [Collection("ManagerCollection")]
    public class ClientTests
    {
        readonly ApplicationManager Manager;
        const int TestTimeout = 5000;

        public ClientTests(ManagerFixture fixture)
        {
            Manager = fixture.Manager;
        }

        [Fact]
        public void SetAdditionalPropertyShouldSucceed()
        {
            var client = new Player();
            int newProp = 5;
            client.SetAdditionalProperty("NewProp", newProp);
        }

        [Fact]
        public void GetAdditionalPropertyShouldSucceed()
        {
            var client = new Player();
            int newProp = 5;
            client.SetAdditionalProperty("NewProp", newProp);

            Assert.True(client.GetAdditionalProperty<int>("NewProp") == 5, "added property does not match retrieved property");
        }

        [Fact]
        public void WarnPlayerShouldSucceed()
        {
            while (!Manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = Manager.Servers.First().GetPlayersAsList().FirstOrDefault();

            Assert.False(client == null, "no client found to warn");

            var warnEvent = client.Warn("test warn", new Player() { ClientId = 1, Level = Player.Permission.Console });
            warnEvent.OnProcessed.Wait(TestTimeout);

            Assert.True(client.Warnings == 1 || 
                warnEvent.Failed, "warning did not get applied");

            warnEvent = client.Warn("test warn", new Player() { ClientId = 1, Level = Player.Permission.Banned });
            warnEvent.OnProcessed.Wait(TestTimeout);

            Assert.True(warnEvent.FailReason == GameEvent.EventFailReason.Permission &&
                client.Warnings == 1, "warning was applied without proper permissions");
        }

        [Fact]
        public void ReportPlayerShouldSucceed()
        {
            while (!Manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = Manager.Servers.First().GetPlayersAsList().FirstOrDefault();

            Assert.False(client == null, "no client found to report");

            // succeed
            var reportEvent = client.Report("test report", new Player() { ClientId = 1, Level = Player.Permission.Console });
            reportEvent.OnProcessed.Wait(TestTimeout);

            Assert.True(!reportEvent.Failed &&
                client.CurrentServer.Reports.Count(r => r.Target.NetworkId == client.NetworkId) == 1, $"report was not applied [{reportEvent.FailReason.ToString()}]");

            // fail
            reportEvent = client.Report("test report", new Player() { ClientId = 2, Level = Player.Permission.Banned });

            Assert.True(reportEvent.FailReason == GameEvent.EventFailReason.Permission &&
               client.CurrentServer.Reports.Count(r => r.Target.NetworkId == client.NetworkId) == 1, $"report was applied without proper permission");

            // fail
            reportEvent = client.Report("test report", client);

            Assert.True(reportEvent.FailReason == GameEvent.EventFailReason.Invalid &&
               client.CurrentServer.Reports.Count(r => r.Target.NetworkId == client.NetworkId) == 1, $"report was applied to self");

            // fail
            reportEvent = client.Report("test report", new Player() { ClientId = 1, Level = Player.Permission.Console});

            Assert.True(reportEvent.FailReason == GameEvent.EventFailReason.Exception &&
               client.CurrentServer.Reports.Count(r => r.Target.NetworkId == client.NetworkId) == 1, $"duplicate report was applied");
        }
    }
}
