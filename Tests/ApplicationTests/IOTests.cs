using FakeItEasy;
using IW4MAdmin.Application.IO;
using NUnit.Framework;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Threading.Tasks;

namespace ApplicationTests
{
    [TestFixture]
    public class IOTests
    {

        [Test]
        public async Task GameLogEventDetection_WorksAfterFileSizeReset()
        {
            var reader = A.Fake<IGameLogReader>();
            var detect = new GameLogEventDetection(null, "", A.Fake<Uri>(), reader);

            A.CallTo(() => reader.Length)
                .Returns(100)
                .Once()
                .Then
                .Returns(200)
                .Once()
                .Then
                .Returns(10)
                .Once()
                .Then
                .Returns(100);

            for (int i = 0; i < 4; i++)
            {
                await detect.UpdateLogEvents();
            }

            A.CallTo(() => reader.ReadEventsFromLog(A<Server>.Ignored, A<long>.Ignored, A<long>.Ignored))
                .MustHaveHappenedTwiceExactly();
        }
    }
}
