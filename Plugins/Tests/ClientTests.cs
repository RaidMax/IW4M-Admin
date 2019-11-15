using IW4MAdmin.Application;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Tests
{
    [Collection("ManagerCollection")]
    public class ClientTests
    {
        private readonly ApplicationManager _manager;
        const int TestTimeout = 10000;

        public ClientTests(ManagerFixture fixture)
        {
            _manager = fixture.Manager;
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
        public void BanEvasionShouldLink()
        {
            var server = _manager.Servers[0];
            var waiter = new ManualResetEventSlim();

            _manager.GetApplicationSettings().Configuration().RConPollRate = 5000;


            while (!server.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var e  = new GameEvent()
            {
                Type = GameEvent.EventType.PreConnect,
                Owner = server,
                Origin = new EFClient()
                {
                    NetworkId = 1337,
                    ClientNumber = 0,
                    CurrentAlias = new EFAlias()
                    {
                        Name = "Ban Me",
                        IPAddress = 1337
                    }
                }
            };

            _manager.GetEventHandler().AddEvent(e);
            e.Complete();

            e = new GameEvent()
            {
                Type = GameEvent.EventType.PreConnect,
                Owner = server,
                Origin = new EFClient()
                {
                    NetworkId = 1338,
                    ClientNumber = 1,
                    CurrentAlias = new EFAlias()
                    {
                        Name = "Ban Me",
                        IPAddress = null
                    }
                }
            };

            _manager.GetEventHandler().AddEvent(e);
            e.Complete();

            e = new GameEvent()
            {
                Type = GameEvent.EventType.Update,
                Owner = server,
                Origin = new EFClient()
                {
                    NetworkId = 1338,
                    ClientNumber = 1,
                    CurrentAlias = new EFAlias()
                    {
                        Name = "Ban Me",
                        IPAddress = 1337
                    }
                }
            };

            _manager.GetEventHandler().AddEvent(e);
            e.Complete();

        }

        [Fact]
        public void WarnClientShouldSucceed()
        {
            var onJoined = new ManualResetEventSlim();
            var server = _manager.Servers[0];

            while (!server.IsInitialized)
            {
                Thread.Sleep(100);
            }

            //_manager.OnServerEvent += (sender, eventArgs) =>
            //{
            //    if (eventArgs.Event.Type == GameEvent.EventType.Connect)
            //    {
            //        onJoined.Set();
            //    }
            //};

            server.EmulateClientJoinLog();
            onJoined.Wait();

            var client = server.Clients[0];

            var warnEvent = client.Warn("test warn", Utilities.IW4MAdminClient(server));
            warnEvent.WaitAsync(new TimeSpan(0, 0, 10), new CancellationToken()).Wait();

            Assert.False(warnEvent.Failed);

            warnEvent = client.Warn("test warn", new EFClient() { ClientId = 1, Level = EFClient.Permission.Banned, CurrentServer = client.CurrentServer });
            warnEvent.WaitAsync(new TimeSpan(0, 0, 10), new CancellationToken()).Wait();

            Assert.True(warnEvent.FailReason == GameEvent.EventFailReason.Permission &&
                client.Warnings == 1, "warning was applied without proper permissions");

            // warn clear
            var warnClearEvent = client.WarnClear(new EFClient { ClientId = 1, Level = EFClient.Permission.Banned, CurrentServer = client.CurrentServer });
            warnClearEvent.WaitAsync(new TimeSpan(0, 0, 10), new CancellationToken()).Wait();

            Assert.True(warnClearEvent.FailReason == GameEvent.EventFailReason.Permission &&
                client.Warnings == 1, "warning was removed without proper permissions");

            warnClearEvent = client.WarnClear(Utilities.IW4MAdminClient(server));
            warnClearEvent.WaitAsync(new TimeSpan(0, 0, 10), new CancellationToken()).Wait();

            Assert.True(!warnClearEvent.Failed && client.Warnings == 0, "warning was not cleared");
        }

        [Fact]
        public void ReportClientShouldSucceed()
        {
            while (!_manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = _manager.Servers.First().GetClientsAsList().FirstOrDefault();
            Assert.False(client == null, "no client found to report");

            // fail
            var player = new EFClient() { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer };
            player.SetAdditionalProperty("_reportCount", 3);
            var reportEvent = client.Report("test report", player);
            reportEvent.WaitAsync(new TimeSpan(0, 0, 10), new CancellationToken()).Wait();

            Assert.True(reportEvent.FailReason == GameEvent.EventFailReason.Throttle &
                client.CurrentServer.Reports.Count(r => r.Target.NetworkId == client.NetworkId) == 0, $"too many reports were applied [{reportEvent.FailReason.ToString()}]");

            // succeed
            reportEvent = client.Report("test report", new EFClient() { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            reportEvent.WaitAsync(new TimeSpan(0, 0, 10), new CancellationToken()).Wait();

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
            while (!_manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = _manager.Servers.First().GetClientsAsList().FirstOrDefault();
            Assert.False(client == null, "no client found to flag");

            var flagEvent = client.Flag("test flag", new EFClient { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            flagEvent.Complete();

            // succeed 
            Assert.True(!flagEvent.Failed &&
                client.Level == EFClient.Permission.Flagged, $"player is not flagged [{flagEvent.FailReason.ToString()}]");
            Assert.False(client.ReceivedPenalties.FirstOrDefault(p => p.Offense == "test flag") == null, "flag was not applied");

            flagEvent = client.Flag("test flag", new EFClient { ClientId = 1, Level = EFClient.Permission.Banned, CurrentServer = client.CurrentServer });
            flagEvent.Complete();

            // fail
            Assert.True(client.ReceivedPenalties.Count == 1, "flag was applied without permisions");

            flagEvent = client.Flag("test flag", new EFClient { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            flagEvent.Complete();

            // fail
            Assert.True(client.ReceivedPenalties.Count == 1, "duplicate flag was applied");

            var unflagEvent = client.Unflag("test unflag", new EFClient { ClientId = 1, Level = EFClient.Permission.Banned, CurrentServer = client.CurrentServer });
            unflagEvent.Complete();

            // fail
            Assert.False(client.Level == EFClient.Permission.User, "user was unflagged without permissions");

            unflagEvent = client.Unflag("test unflag", new EFClient { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            unflagEvent.Complete();

            // succeed
            Assert.True(client.Level == EFClient.Permission.User, "user was not unflagged");

            unflagEvent = client.Unflag("test unflag", new EFClient { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            unflagEvent.Complete();

            // succeed
            Assert.True(unflagEvent.FailReason == GameEvent.EventFailReason.Invalid, "user was not flagged");
        }

        [Fact]
        void KickClientShouldSucceed()
        {
            while (!_manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = _manager.Servers.First().GetClientsAsList().FirstOrDefault();
            Assert.False(client == null, "no client found to kick");

            var kickEvent = client.Kick("test kick", new EFClient() { ClientId = 1, Level = EFClient.Permission.Banned, CurrentServer = client.CurrentServer });
            kickEvent.Complete();

            Assert.True(kickEvent.FailReason == GameEvent.EventFailReason.Permission, "client was kicked without permission");

            kickEvent = client.Kick("test kick", new EFClient() { ClientId = 1, Level = EFClient.Permission.Console, CurrentServer = client.CurrentServer });
            kickEvent.Complete();

            Assert.True(_manager.Servers.First().GetClientsAsList().FirstOrDefault(c => c.NetworkId == client.NetworkId) == null, "client was not kicked");
        }

        [Fact]
        void TempBanClientShouldSucceed()
        {
            while (!_manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = _manager.Servers.First().GetClientsAsList().FirstOrDefault();
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

            Assert.True(_manager.GetPenaltyService().GetActivePenaltiesAsync(client.AliasLinkId).Result.Count(p => p.Type == EFPenalty.PenaltyType.TempBan) == 1,
                "tempban was not added");
        }

        [Fact]
        void BanUnbanClientShouldSucceed()
        {
            while (!_manager.IsInitialized)
            {
                Thread.Sleep(100);
            }

            var client = _manager.Servers.First().GetClientsAsList().FirstOrDefault();
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

            Assert.True(_manager.GetPenaltyService().GetActivePenaltiesAsync(client.AliasLinkId).Result.Count(p => p.Type == EFPenalty.PenaltyType.Ban) == 1,
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

            Assert.True(_manager.GetPenaltyService().GetActivePenaltiesAsync(client.AliasLinkId).Result.Count(p => p.Type == EFPenalty.PenaltyType.Ban) == 0,
                "ban was not removed");

        }
    }
}
