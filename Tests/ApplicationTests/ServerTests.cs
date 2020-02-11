using FakeItEasy;
using IW4MAdmin;
using IW4MAdmin.Application;
using IW4MAdmin.Application.EventParsers;
using NUnit.Framework;
using SharedLibraryCore.Interfaces;
using System;
using System.Diagnostics;

namespace ApplicationTests
{
    [TestFixture]
    public class ServerTests
    {
        ILogger logger;

        [SetUp]
        public void Setup()
        {
            logger = A.Fake<ILogger>();

            void testLog(string msg) => Console.WriteLine(msg);

            A.CallTo(() => logger.WriteError(A<string>.Ignored)).Invokes((string msg) => testLog(msg));
            A.CallTo(() => logger.WriteWarning(A<string>.Ignored)).Invokes((string msg) => testLog(msg));
            A.CallTo(() => logger.WriteInfo(A<string>.Ignored)).Invokes((string msg) => testLog(msg));
            A.CallTo(() => logger.WriteDebug(A<string>.Ignored)).Invokes((string msg) => testLog(msg));
        }

        [Test]
        public void GameTimeFalseQuitTest()
        {
            var mgr = A.Fake<IManager>();
            var server = new IW4MServer(mgr,
                new SharedLibraryCore.Configuration.ServerConfiguration() { IPAddress = "127.0.0.1", Port = 28960 },
                A.Fake<ITranslationLookup>(), A.Fake<IRConConnectionFactory>());

            var parser = new BaseEventParser();
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
            A.CallTo(() => mgr.GetLogger(A<long>.Ignored)).Returns(logger);

            var server = new IW4MServer(mgr,
                new SharedLibraryCore.Configuration.ServerConfiguration() { IPAddress = "127.0.0.1", Port = 28960 },
                A.Fake<ITranslationLookup>(), A.Fake<IRConConnectionFactory>());

            var parser = new BaseEventParser();
            parser.Configuration.GuidNumberStyle = System.Globalization.NumberStyles.Integer;

            var log = System.IO.File.ReadAllLines("Files\\T6Game.log");
            long lastEventId = 0;
            foreach (string line in log)
            {
                var e = parser.GenerateGameEvent(line);
                server.Logger.WriteInfo($"{e.GameTime}");
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