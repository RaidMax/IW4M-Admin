using ApplicationTests.Fixtures;
using IW4MAdmin.Application.EventParsers;
using IW4MAdmin.Application.Factories;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using NUnit.Framework;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;

namespace ApplicationTests
{
    [TestFixture]
    public class BaseEventParserTests
    {
        private EventLogTest eventLogData;
        private IServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            eventLogData = JsonConvert.DeserializeObject<EventLogTest>(System.IO.File.ReadAllText("Files/GameEvents.json"));
            serviceProvider = new ServiceCollection()
                .AddSingleton<BaseEventParser>()
                .AddTransient<IParserPatternMatcher, ParserPatternMatcher>()
                .AddSingleton<IParserRegexFactory, ParserRegexFactory>()
                .BuildServiceProvider();
        }

        [Test]
        public void TestParsesAllEventData()
        {
            var eventParser = serviceProvider.GetService<BaseEventParser>();

            void AssertMatch(GameEvent src, LogEvent expected)
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

            foreach (var e in eventLogData.Events)
            {
                var parsedEvent = eventParser.GenerateGameEvent(e.EventLine);
                AssertMatch(parsedEvent, e);
            }
        }
    }
}
