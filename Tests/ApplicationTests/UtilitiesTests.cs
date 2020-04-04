using NUnit.Framework;
using SharedLibraryCore;

namespace ApplicationTests
{
    [TestFixture]
    public class UtilitiesTests
    {
        [Test]
        public void TestCapClientNameLengthReachesMax()
        {
            string originalName = "SomeVeryLongName";
            string expectedName = "SomeVeryLong...";
            int maxLength = originalName.Length - 1;

            string cappedName = originalName.CapClientName(maxLength);

            Assert.AreEqual(expectedName, cappedName);
        }

        [Test]
        public void TestCapClientNameRetainsOriginal()
        {
            string originalName = "Short";
            int maxLength = originalName.Length;

            string cappedName = originalName.CapClientName(maxLength);

            Assert.AreEqual(originalName, cappedName);
        }
    }
}
