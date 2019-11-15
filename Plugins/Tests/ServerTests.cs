using IW4MAdmin.Application;
using SharedLibraryCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Tests
{
    [Collection("ManagerCollection")]
    public class ServerTests
    {
        private readonly ApplicationManager _manager;

        public ServerTests(ManagerFixture fixture)
        {
            _manager = fixture.Manager;
        }

        [Fact]
        public void AddAndRemoveClientViaLog()
        {
            var resetEvent = new ManualResetEventSlim();
            var server = _manager.Servers[0];

            var currentClientCount = server.ClientNum;
            int eventsProcessed = 0;

            /*_manager.OnServerEvent += (sender, eventArgs) =>
            {
                if (eventArgs.Event.Type == GameEvent.EventType.Connect)
                {
                    eventArgs.Event.Complete();
                    Assert.False(eventArgs.Event.Failed, "connect event was not processed");
                    Assert.True(server.ClientNum == currentClientCount  + 1, "client count was not incremented");
                    eventsProcessed++;
                    resetEvent.Set();
                }

                if (eventArgs.Event.Type == GameEvent.EventType.Disconnect)
                {
                    eventArgs.Event.Complete();
                    Assert.False(eventArgs.Event.Failed, "disconnect event was not processed");
                    Assert.True(server.ClientNum == currentClientCount, "client count was not decremented");
                    eventsProcessed++;
                    resetEvent.Set();
                }
            };*/

            server.EmulateClientJoinLog();

            resetEvent.Wait(15000);
            resetEvent.Reset();

            Assert.Equal(1, eventsProcessed);

            server.EmulateClientQuitLog();

            resetEvent.Wait(15000);

            Assert.Equal(2, eventsProcessed);
        }

        [Fact]
        public void AddAndRemoveClientViaRcon()
        {
            var resetEvent = new ManualResetEventSlim();
            var server = _manager.Servers[0];

            var currentClientCount = server.ClientNum;
            int eventsProcessed = 0;

            _manager.GetApplicationSettings().Configuration().RConPollRate = 5000;
            /*_manager.OnServerEvent += (sender, eventArgs) =>
            {
                if (eventArgs.Event.Type == GameEvent.EventType.Connect)
                {
                    eventArgs.Event.Complete();
                    Assert.False(eventArgs.Event.Failed, "connect event was not processed");
                    Assert.True(server.ClientNum == currentClientCount + 1, "client count was not incremented");
                    eventsProcessed++;
                    resetEvent.Set();
                }

                if (eventArgs.Event.Type == GameEvent.EventType.Disconnect)
                {
                    eventArgs.Event.Complete();
                    Assert.False(eventArgs.Event.Failed, "disconnect event was not processed");
                    Assert.True(server.ClientNum == currentClientCount, "client count was not decremented");
                    eventsProcessed++;
                    resetEvent.Set();
                }
            };*/

            (server.RconParser as TestRconParser).FakeClientCount = 1;

            resetEvent.Wait(15000);
            resetEvent.Reset();

            Assert.Equal(1, eventsProcessed);

            (server.RconParser as TestRconParser).FakeClientCount = 0;

            resetEvent.Wait(15000);

            Assert.Equal(2, eventsProcessed);

            _manager.GetApplicationSettings().Configuration().RConPollRate = int.MaxValue;
        }
    }
}
