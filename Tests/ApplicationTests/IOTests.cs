using FakeItEasy;
using IW4MAdmin.Application.IO;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using SharedLibraryCore.Interfaces;
using System;
using System.Threading.Tasks;
using SharedLibraryCore;

namespace ApplicationTests
{
    [TestFixture]
    public class IOTests
    {

        private IServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            serviceProvider = new ServiceCollection()
                .BuildBase()
                .AddSingleton(new Uri[] { new Uri("C:\\test.log")})
                .AddSingleton(A.Fake<IGameLogReaderFactory>())
                .AddSingleton<GameLogEventDetection>()
                .BuildServiceProvider();
        }

        [Test]
        public async Task GameLogEventDetection_WorksAfterFileSizeReset()
        {
            var reader = A.Fake<IGameLogReader>();
            var factory = serviceProvider.GetRequiredService<IGameLogReaderFactory>();

            A.CallTo(() => factory.CreateGameLogReader(A<Uri[]>.Ignored, A<IEventParser>.Ignored))
                .Returns(reader);

            var detect = serviceProvider.GetRequiredService<GameLogEventDetection>();

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

            A.CallTo(() => reader.ReadEventsFromLog(A<long>.Ignored, A<long>.Ignored))
                .MustHaveHappenedTwiceExactly();
        }
    }
}
