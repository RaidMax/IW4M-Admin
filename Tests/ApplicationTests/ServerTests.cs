using FakeItEasy;
using IW4MAdmin;
using IW4MAdmin.Application.EventParsers;
using NUnit.Framework;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using Microsoft.Extensions.DependencyInjection;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ApplicationTests
{
    [TestFixture]
    public class ServerTests
    {
        private IServiceProvider _serviceProvider;
        
        [SetUp]
        public void Setup()
        {
            _serviceProvider = new ServiceCollection()
                .BuildBase()
                .BuildServiceProvider();
        }

        [Test]
        public void GameTimeFalseQuitTest()
        {
            var server = _serviceProvider.GetRequiredService<IW4MServer>();
            var parser = new BaseEventParser(A.Fake<IParserRegexFactory>(), A.Fake<ILogger>(), A.Fake<ApplicationConfiguration>());
            parser.Configuration.GuidNumberStyle = System.Globalization.NumberStyles.Integer;

            var log = System.IO.File.ReadAllLines("Files\\T6MapRotation.log");
            foreach (string line in log)
            {
                var e = parser.GenerateGameEvent(line);
                if (e.Origin != null)
                {
                    e.Origin.CurrentServer = server;
                }

                server.ExecuteEvent(e).Wait();
            }
        }

        [Test]
        public void LogFileReplay()
        {
            var server = _serviceProvider.GetRequiredService<IW4MServer>();
            var parser = new BaseEventParser(A.Fake<IParserRegexFactory>(), A.Fake<ILogger>(), A.Fake<ApplicationConfiguration>());
            parser.Configuration.GuidNumberStyle = System.Globalization.NumberStyles.Integer;

            var log = System.IO.File.ReadAllLines("Files\\T6Game.log");
            long lastEventId = 0;
            foreach (string line in log)
            {
                var e = parser.GenerateGameEvent(line);
                if (e.Origin != null)
                {
                    e.Origin.CurrentServer = server;
                }

                server.ExecuteEvent(e).Wait();
                lastEventId = e.Id;
            }

            Assert.GreaterOrEqual(lastEventId, log.Length);
        }
    }
}