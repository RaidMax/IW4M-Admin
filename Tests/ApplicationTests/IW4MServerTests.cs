using IW4MAdmin;
using IW4MAdmin.Application.Misc;
using NUnit.Framework;

namespace ApplicationTests
{
    [TestFixture]
    public class IW4MServerTests
    {
        [Test]
        public void Test_GenerateLogPath_Basic()
        {
            string expected = "C:\\Game\\main\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BasePathDirectory = "C:\\Game",
                GameDirectory = "main",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_WithMod()
        {
            string expected = "C:\\Game\\mods\\mod\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BasePathDirectory = "C:\\Game",
                GameDirectory = "main",
                ModDirectory = "mods\\mod",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_WithBaseGame()
        {
            string expected = "C:\\GameAlt\\main\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BaseGameDirectory = "C:\\GameAlt",
                BasePathDirectory = "C:\\Game",
                GameDirectory = "main",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_WithBaseGameAndMod()
        {
            string expected = "C:\\GameAlt\\mods\\mod\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BaseGameDirectory = "C:\\GameAlt",
                BasePathDirectory = "C:\\Game",
                GameDirectory = "main",
                ModDirectory = "mods\\mod",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_InvalidBasePath()
        {
            string expected = "C:\\Game\\main\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BaseGameDirectory = "game",
                BasePathDirectory = "C:\\Game",
                GameDirectory = "main",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_BadSeparators()
        {
            string expected = "C:\\Game\\main\\folder\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BasePathDirectory = "C:/Game",
                GameDirectory = "main/folder",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_RelativeBasePath()
        {
            string expected = "C:\\Game\\main\\folder\\log.log";
            var info = new LogPathGeneratorInfo()
            {
                BaseGameDirectory = "main\\folder",
                BasePathDirectory = "C:\\Game",
                GameDirectory = "main\\folder",
                LogFile = "log.log"
            };
            string generated = IW4MServer.GenerateLogPath(info);

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_FixWineDriveMangling()
        {
            string expected = "/opt/server/game/log.log";
            var info = new LogPathGeneratorInfo()
            {
                BasePathDirectory = "Z:\\opt\\server",
                GameDirectory = "game",
                LogFile = "log.log",
                IsWindows = false
            };
            string generated = IW4MServer.GenerateLogPath(info).Replace('\\', '/');

            Assert.AreEqual(expected, generated);
        }
    }
}
