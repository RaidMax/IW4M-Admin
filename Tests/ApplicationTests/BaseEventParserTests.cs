using ApplicationTests.Fixtures;
using FakeItEasy;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.Factories;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Interfaces;
using System;
using static SharedLibraryCore.GameEvent;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ApplicationTests
{
    [TestFixture]
    public class BaseEventParserTests
    {
        private EventLogTest eventLogData;
        private IServiceProvider serviceProvider;
        private ApplicationConfiguration appConfig;

        [SetUp]
        public void Setup()
        {
            eventLogData = JsonConvert.DeserializeObject<EventLogTest>(System.IO.File.ReadAllText("Files/GameEvents.json"));
            appConfig = ConfigurationGenerators.CreateApplicationConfiguration();

            serviceProvider = new ServiceCollection()
                .AddSingleton(A.Fake<ILogger>())
                .AddSingleton<BaseEventParser>()
                .AddTransient<IParserPatternMatcher, ParserPatternMatcher>()
                .AddSingleton<IParserRegexFactory, ParserRegexFactory>()
                .AddSingleton(appConfig)
                .BuildServiceProvider();
        }

        [Test]
        public void TestParsesAllEventData()
        {
            var eventParser = serviceProvider.GetService<BaseEventParser>();

            foreach (var e in eventLogData.Events)
            {
                var parsedEvent = eventParser.GenerateGameEvent(e.EventLine);
                AssertMatch(parsedEvent, e);
            }
        }

        [Test]
        public void TestCustomEvents()
        {
            var eventParser = serviceProvider.GetService<BaseEventParser>();
            string eventMessage = "Hello this is my test event message";
            string triggerValue = "testTrigger";
            string eventType = "testType";

            eventParser.RegisterCustomEvent(eventType, triggerValue, (logLine, config, generatedEvent) =>
            {
                generatedEvent.Message = eventMessage;
                return generatedEvent;
            });

            var customEvent = eventParser.GenerateGameEvent($"23:14 {triggerValue}");

            Assert.AreEqual(EventType.Other, customEvent.Type);
            Assert.AreEqual(eventType, customEvent.Subtype);
            Assert.AreEqual(eventMessage, customEvent.Message);
        }

        [Test]
        public void TestCustomEventRegistrationArguments()
        {
            var eventParser = serviceProvider.GetService<BaseEventParser>();

            Assert.Throws<ArgumentException>(() => eventParser.RegisterCustomEvent(null, null, null));
            Assert.Throws<ArgumentException>(() => eventParser.RegisterCustomEvent("test", null, null));
            Assert.Throws<ArgumentException>(() => eventParser.RegisterCustomEvent("test", "test2", null));
            Assert.Throws<ArgumentException>(() =>
            {
                // testing duplicate registers
                eventParser.RegisterCustomEvent("test", "test", (a, b, c) => new GameEvent());
                eventParser.RegisterCustomEvent("test", "test", (a, b, c) => new GameEvent());
            });
        }

        [Test]
        public void Test_CustomCommandPrefix_Parses()
        {
            var eventParser = serviceProvider.GetService<BaseEventParser>();
            var commandData = JsonConvert.DeserializeObject<EventLogTest>(System.IO.File.ReadAllText("Files/GameEvent.Command.CustomPrefix.json"));
            appConfig.CommandPrefix = "^^";

            var e = commandData.Events[0];
            var parsedEvent = eventParser.GenerateGameEvent(e.EventLine);
            AssertMatch(parsedEvent, e);
        }

        [Test]
        public void Test_CustomBroadcastCommandPrefix_Parses()
        {
            var eventParser = serviceProvider.GetService<BaseEventParser>();
            var commandData = JsonConvert.DeserializeObject<EventLogTest>(System.IO.File.ReadAllText("Files/GameEvent.Command.CustomPrefix.json"));
            appConfig.BroadcastCommandPrefix = "@@";

            var e = commandData.Events[1];
            var parsedEvent = eventParser.GenerateGameEvent(e.EventLine);
            AssertMatch(parsedEvent, e);
        }

        [TestCase("2026-08-23T19:20:11 123.456 INF GMSG: Player 'Victim' killed by 'Attacker'", "Attacker")]
        [TestCase("2026-08-23T19:20:11 123.456 INF GMSG: Player 'Victim' died", null)]
        public void Test_SevenDaysToDieNameOnlyKillEvent_Parses(string logLine, string expectedAttacker)
        {
            var eventParser = serviceProvider.GetRequiredService<BaseEventParser>();
            eventParser.GameName = Server.Game.D7D;
            eventParser.Configuration.Time.Pattern =
                @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d+)?\s+\d+(?:\.\d+)?\s+(?:INF|WRN|ERR|EXC)\s+";
            eventParser.Configuration.Kill.Pattern =
                @"^GMSG: Player '(.+)' (?:(?:killed by '(.+)')|died)\s*$";
            eventParser.Configuration.Kill.GroupMapping.Clear();
            eventParser.Configuration.Kill.AddMapping(ParserRegex.GroupType.TargetName, 1);
            eventParser.Configuration.Kill.AddMapping(ParserRegex.GroupType.OriginName, 2);

            var parsedEvent = eventParser.GenerateGameEvent(logLine) as ClientKillEvent;

            Assert.That(parsedEvent, Is.Not.Null);
            Assert.That(parsedEvent.Type, Is.EqualTo(EventType.Kill));
            Assert.That(parsedEvent.AttackerClientName, Is.EqualTo(expectedAttacker));
            Assert.That(parsedEvent.VictimClientName, Is.EqualTo("Victim"));
            Assert.That(parsedEvent.Attacker.NetworkId, Is.EqualTo(Utilities.WORLD_ID));
            Assert.That(parsedEvent.Victim.NetworkId, Is.EqualTo(Utilities.WORLD_ID));
        }

        private static void AssertMatch(GameEvent src, LogEvent expected)
        {
            Assert.AreEqual(expected.ExpectedEventType, src.Type);
            Assert.AreEqual(expected.ExpectedData, src.Data);
            Assert.AreEqual(expected.ExpectedMessage, src.Message);
            Assert.AreEqual(expected.ExpectedTime, src.GameTime);

            //Assert.AreEqual(expected.ExpectedOriginClientName, src.Origin?.Name);
            Assert.AreEqual(expected.ExpectedOriginClientNumber, src.Origin?.ClientNumber);
            Assert.AreEqual(expected.ExpectedOriginNetworkId, src.Origin?.NetworkId.ToString("X"));

            //Assert.AreEqual(expected.ExpectedTargetClientName, src.Target?.Name);
            Assert.AreEqual(expected.ExpectedTargetClientNumber, src.Target?.ClientNumber);
            Assert.AreEqual(expected.ExpectedTargetNetworkId, src.Target?.NetworkId.ToString("X"));
        }
    }
}
