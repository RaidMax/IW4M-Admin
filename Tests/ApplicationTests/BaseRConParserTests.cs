using FakeItEasy;
using IW4MAdmin.Application.RconParsers;
using NUnit.Framework;
using SharedLibraryCore.Interfaces;

namespace ApplicationTests
{
    [TestFixture]
    public class BaseRConParserTests
    {
        [Test]
        public void SetDvarAsync_FormatStringType()
        {
            var parser = new BaseRConParser(A.Fake<IParserRegexFactory>());
            var connection = A.Fake<IRConConnection>();

            parser.SetDvarAsync(connection, "test", "test").Wait();

            A.CallTo(() => connection.SendQueryAsync(SharedLibraryCore.RCon.StaticHelpers.QueryType.SET_DVAR, "test \"test\""))
                .MustHaveHappened();
        }

        [Test]
        public void SetDvarAsync_FormatEmptyStringTypeIncludesQuotes()
        {
            var parser = new BaseRConParser(A.Fake<IParserRegexFactory>());
            var connection = A.Fake<IRConConnection>();

            parser.SetDvarAsync(connection, "test", "").Wait();

            A.CallTo(() => connection.SendQueryAsync(SharedLibraryCore.RCon.StaticHelpers.QueryType.SET_DVAR, "test \"\""))
                .MustHaveHappened();
        }

        [Test]
        public void SetDvarAsync_FormatsNonString()
        {
            var parser = new BaseRConParser(A.Fake<IParserRegexFactory>());
            var connection = A.Fake<IRConConnection>();

            parser.SetDvarAsync(connection, "test", 123).Wait();

            A.CallTo(() => connection.SendQueryAsync(SharedLibraryCore.RCon.StaticHelpers.QueryType.SET_DVAR, "test 123"))
                .MustHaveHappened();
        }
    }
}
