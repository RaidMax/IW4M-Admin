using IW4MAdmin.Application;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Database.Models;
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
        const int TestTimeout = 10000;

        public ClientTests(ManagerFixture fixture)
        {
            Manager = fixture.Manager;
        }

        [Fact]
        public void SetAdditionalPropertyShouldSucceed()
        {
            var client = new EFClient();
            int newProp = 5;
            client.SetAdditionalProperty("NewProp", newProp);
        }

        [Fact]
        public void GetAdditionalPropertyShouldSucceed()
        {
            var client = new EFClient();
            int newProp = 5;
            client.SetAdditionalProperty("NewProp", newProp);

            Assert.True(client.GetAdditionalProperty<int>("NewProp") == 5, "added property does not match retrieved property");
        }

        [Fact]
        public void WarnClientShouldSucceed()
        {
            while (!Manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = Manager.Servers.First().GetClientsAsList().FirstOrDefault();

            Assert.False(client == null, "no client found to warn");

            var warnEvent = client.Warn("test warn", new EFClient() { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            warnEvent.OnProcessed.Wait();

            //Assert.True((client.Warnings == 1 ||
            //    warnEvent.Failed) &&
            //    Manager.GetPenaltyService().GetClientPenaltiesAsync(client.ClientId).Result.Count(p => p.Type == Penalty.PenaltyType.Warning) == 1,
            //    "warning did not get applied");

            warnEvent = client.Warn("test warn", new EFClient() { ClientId = 1, Level = EFClient.Permission.Banned, CurrentServer = client.CurrentServer });
            warnEvent.OnProcessed.Wait();

            Assert.True(warnEvent.FailReason == GameEvent.EventFailReason.Permission &&
                client.Warnings == 1, "warning was applied without proper permissions");

            // warn clear
            var warnClearEvent = client.WarnClear(new EFClient { ClientId = 1, Level = EFClient.Permission.Banned, CurrentServer = client.CurrentServer });

            Assert.True(warnClearEvent.FailReason == GameEvent.EventFailReason.Permission &&
                client.Warnings == 1, "warning was removed without proper permissions");

            warnClearEvent = client.WarnClear(new EFClient { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });

            Assert.True(!warnClearEvent.Failed && client.Warnings == 0, "warning was not cleared");
        }

        [Fact]
        public void ReportClientShouldSucceed()
        {
            while (!Manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = Manager.Servers.First().GetClientsAsList().FirstOrDefault();
            Assert.False(client == null, "no client found to report");

            // fail
            var player = new EFClient() { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer };
            player.SetAdditionalProperty("_reportCount", 3);
            var reportEvent = client.Report("test report", player);
            reportEvent.OnProcessed.Wait(TestTimeout);

            Assert.True(reportEvent.FailReason == GameEvent.EventFailReason.Throttle &
                client.CurrentServer.Reports.Count(r => r.Target.NetworkId == client.NetworkId) == 0, $"too many reports were applied [{reportEvent.FailReason.ToString()}]");

            // succeed
            reportEvent = client.Report("test report", new EFClient() { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            reportEvent.OnProcessed.Wait(TestTimeout);

            Assert.True(!reportEvent.Failed &&
                client.CurrentServer.Reports.Count(r => r.Target.NetworkId == client.NetworkId) == 1, $"report was not applied [{reportEvent.FailReason.ToString()}]");

            // fail
            reportEvent = client.Report("test report", new EFClient() { ClientId = 1, NetworkId = 1, Level = EFClient.Permission.Banned, CurrentServer = client.CurrentServer });

            Assert.True(reportEvent.FailReason == GameEvent.EventFailReason.Permission &&
               client.CurrentServer.Reports.Count(r => r.Target.NetworkId == client.NetworkId) == 1,
               $"report was applied without proper permission [{reportEvent.FailReason.ToString()},{ client.CurrentServer.Reports.Count(r => r.Target.NetworkId == client.NetworkId)}]");

            // fail
            reportEvent = client.Report("test report", client);

            Assert.True(reportEvent.FailReason == GameEvent.EventFailReason.Invalid &&
               client.CurrentServer.Reports.Count(r => r.Target.NetworkId == client.NetworkId) == 1, $"report was applied to self");

            // fail
            reportEvent = client.Report("test report", new EFClient() { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });

            Assert.True(reportEvent.FailReason == GameEvent.EventFailReason.Exception &&
               client.CurrentServer.Reports.Count(r => r.Target.NetworkId == client.NetworkId) == 1, $"duplicate report was applied");
        }

        [Fact]
        public void FlagClientShouldSucceed()
        {
            while (!Manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = Manager.Servers.First().GetClientsAsList().FirstOrDefault();
            Assert.False(client == null, "no client found to flag");

            var flagEvent = client.Flag("test flag", new EFClient { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            flagEvent.OnProcessed.Wait();

            // succeed 
            Assert.True(!flagEvent.Failed &&
                client.Level == EFClient.Permission.Flagged, $"player is not flagged [{flagEvent.FailReason.ToString()}]");
            Assert.False(client.ReceivedPenalties.FirstOrDefault(p => p.Offense == "test flag") == null, "flag was not applied");

            flagEvent = client.Flag("test flag", new EFClient { ClientId = 1, Level = EFClient.Permission.Banned, CurrentServer = client.CurrentServer });
            flagEvent.OnProcessed.Wait();

            // fail
            Assert.True(client.ReceivedPenalties.Count == 1, "flag was applied without permisions");

            flagEvent = client.Flag("test flag", new EFClient { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            flagEvent.OnProcessed.Wait();

            // fail
            Assert.True(client.ReceivedPenalties.Count == 1, "duplicate flag was applied");

            var unflagEvent = client.Unflag("test unflag", new EFClient { ClientId = 1, Level = EFClient.Permission.Banned, CurrentServer = client.CurrentServer });
            unflagEvent.OnProcessed.Wait();

            // fail
            Assert.False(client.Level == EFClient.Permission.User, "user was unflagged without permissions");

            unflagEvent = client.Unflag("test unflag", new EFClient { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            unflagEvent.OnProcessed.Wait();

            // succeed
            Assert.True(client.Level == EFClient.Permission.User, "user was not unflagged");

            unflagEvent = client.Unflag("test unflag", new EFClient { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            unflagEvent.OnProcessed.Wait();

            // succeed
            Assert.True(unflagEvent.FailReason == GameEvent.EventFailReason.Invalid, "user was not flagged");
        }

        [Fact]
        void KickClientShouldSucceed()
        {
            while (!Manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = Manager.Servers.First().GetClientsAsList().FirstOrDefault();
            Assert.False(client == null, "no client found to kick");

            var kickEvent = client.Kick("test kick", new EFClient() { ClientId = 1, Level = EFClient.Permission.Banned, CurrentServer = client.CurrentServer });
            kickEvent.OnProcessed.Wait();

            Assert.True(kickEvent.FailReason == GameEvent.EventFailReason.Permission, "client was kicked without permission");

            kickEvent = client.Kick("test kick", new EFClient() { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            kickEvent.OnProcessed.Wait();

            Assert.True(Manager.Servers.First().GetClientsAsList().FirstOrDefault(c => c.NetworkId == client.NetworkId) == null, "client was not kicked");
        }

        [Fact]
        void TempBanClientShouldSucceed()
        {
            while (!Manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = Manager.Servers.First().GetClientsAsList().FirstOrDefault();
            Assert.False(client == null, "no client found to tempban");

            var tbCommand = new CTempBan();
            tbCommand.ExecuteAsync(new GameEvent()
            {
                Origin = new EFClient() { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer },
                Target = client,
                Data = "5days test tempban",
                Type = GameEvent.EventType.Command,
                Owner = client.CurrentServer
            }).Wait();

            Assert.True(Manager.GetPenaltyService().GetActivePenaltiesAsync(client.AliasLinkId).Result.Count(p => p.Type == EFPenalty.PenaltyType.TempBan) == 1,
                "tempban was not added");
        }

        [Fact]
        void BanUnbanClientShouldSucceed()
        {
            while (!Manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = Manager.Servers.First().GetClientsAsList().FirstOrDefault();
            Assert.False(client == null, "no client found to ban");

            var banCommand = new CBan();
            banCommand.ExecuteAsync(new GameEvent()
            {
                Origin = new EFClient() { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer },
                Target = client,
                Data = "test ban",
                Type = GameEvent.EventType.Command,
                Owner = client.CurrentServer
            }).Wait();

            Assert.True(Manager.GetPenaltyService().GetActivePenaltiesAsync(client.AliasLinkId).Result.Count(p => p.Type == EFPenalty.PenaltyType.Ban) == 1,
                "ban was not added");

            var unbanCommand = new CUnban();
            unbanCommand.ExecuteAsync(new GameEvent()
            {
                Origin = new EFClient() { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer },
                //Target = Manager.GetClientService().Find(c => c.NetworkId == client.NetworkId).Result.First(),
                Data = "test unban",
                Type = GameEvent.EventType.Command,
                Owner = client.CurrentServer
            }).Wait();

            Assert.True(Manager.GetPenaltyService().GetActivePenaltiesAsync(client.AliasLinkId).Result.Count(p => p.Type == EFPenalty.PenaltyType.Ban) == 0,
                "ban was not removed");

        }
    }
}
