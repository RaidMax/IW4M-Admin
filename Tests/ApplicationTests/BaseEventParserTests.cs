using ApplicationTests.Fixtures;
using FakeItEasy;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.Factories;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using Microsoft.Extensions.Logging;
using static SharedLibraryCore.GameEvent;

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
