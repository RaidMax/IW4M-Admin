using FakeItEasy;
using IW4MAdmin;
using IW4MAdmin.Application;
using IW4MAdmin.Application.EventParsers;
using NUnit.Framework;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;

namespace ApplicationTests
{
    [TestFixture]
    public class ServerTests
    {

        [SetUp]
        public void Setup()
        {

        }

        [Test]
        public void GameTimeFalseQuitTest()
        {
            var mgr = A.Fake<IManager>();
            var server = new IW4MServer(mgr,
                new SharedLibraryCore.Configuration.ServerConfiguration() { IPAddress = "127.0.0.1", Port = 28960 },
                A.Fake<ITranslationLookup>(), A.Fake<IRConConnectionFactory>(), 
                A.Fake<IGameLogReaderFactory>(), A.Fake<IMetaService>(), A.Fake<ILogger<Server>>());

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
            var mgr = A.Fake<IManager>();

            var server = new IW4MServer(mgr,
                new SharedLibraryCore.Configuration.ServerConfiguration() { IPAddress = "127.0.0.1", Port = 28960 },
                A.Fake<ITranslationLookup>(), A.Fake<IRConConnectionFactory>(), A.Fake<IGameLogReaderFactory>(), A.Fake<IMetaService>(),
                A.Fake<ILogger<Server>>());

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