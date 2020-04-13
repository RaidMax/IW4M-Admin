using IW4MAdmin;
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
            string generated = IW4MServer.GenerateLogPath("", "C:\\Game", "main", null, "log.log");

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_WithMod()
        {
            string expected = "C:\\Game\\mods\\mod\\log.log";
            string generated = IW4MServer.GenerateLogPath("", "C:\\Game", "main", "mods\\mod", "log.log");

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_WithBasePath()
        {
            string expected = "C:\\GameAlt\\main\\log.log";
            string generated = IW4MServer.GenerateLogPath("C:\\GameAlt", "C:\\Game", "main", null, "log.log");

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_WithBasePathAndMod()
        {
            string expected = "C:\\GameAlt\\mods\\mod\\log.log";
            string generated = IW4MServer.GenerateLogPath("C:\\GameAlt", "C:\\Game", "main", "mods\\mod", "log.log");

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_InvalidBasePath()
        {
            string expected = "C:\\Game\\main\\log.log";
            string generated = IW4MServer.GenerateLogPath("game", "C:\\Game", "main", null, "log.log");

            Assert.AreEqual(expected, generated);
        }

        [Test]
        public void Test_GenerateLogPath_BadSeparators()
        {
            string expected = "C:\\Game\\main\\folder\\log.log";
            string generated = IW4MServer.GenerateLogPath("", "C:/Game", "main/folder", null, "log.log");

            Assert.AreEqual(expected, generated);
        }
    }
}
