using ApplicationTests.Fixtures;
using IW4MAdmin;
using IW4MAdmin.Application;
using IW4MAdmin.Application.Misc;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ApplicationTests
{
    [TestFixture]
    public class VcrTests
    {
        private IServiceProvider serviceProvider;

        [SetUp]
        public void Setup()
        {
            serviceProvider = new ServiceCollection().BuildBase()
                .BuildServiceProvider();
        }

        [Test]
        [TestCase("replay")]
        public async Task ReplayEvents(string source)
        {
            var sourceData = await serviceProvider
                .GetRequiredService<DataFileLoader>()
                .Load<EventLog>(source);

            var server = serviceProvider.GetRequiredService<IW4MServer>();

            foreach (var gameEvent in sourceData.Values.First())
            {
                await server.ExecuteEvent(gameEvent);
            }
        }
    }
}
