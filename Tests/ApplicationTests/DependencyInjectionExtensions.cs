using ApplicationTests.Fixtures;
using ApplicationTests.Mocks;
using FakeItEasy;
using IW4MAdmin;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ApplicationTests
{
    static class DependencyInjectionExtensions
    {
        public static IServiceCollection BuildBase(this IServiceCollection serviceCollection, IEventHandler eventHandler = null)
        {

            if (eventHandler == null)
            {
                eventHandler = new EventHandlerMock();
                serviceCollection.AddSingleton(eventHandler as EventHandlerMock);
            }

            else if (eventHandler is EventHandlerMock mockEventHandler)
            {
                serviceCollection.AddSingleton(mockEventHandler);
            }

            var manager = A.Fake<IManager>();

            var transLookup = A.Fake<ITranslationLookup>();
            A.CallTo(() => transLookup[A<string>.Ignored])
                .Returns("test");

            serviceCollection
                .AddLogging()
                .AddSingleton(A.Fake<ILogger>())
                .AddSingleton(A.Fake<SharedLibraryCore.Interfaces.ILogger>())
                .AddSingleton(new ServerConfiguration { IPAddress = "127.0.0.1", Port = 28960 })
                .AddSingleton(manager)
                .AddSingleton<IDatabaseContextFactory, DatabaseContextFactoryMock>()
                .AddSingleton<IW4MServer>()
                .AddSingleton(A.Fake<IRConConnectionFactory>())
                .AddSingleton(A.Fake<IRConConnection>())
                .AddSingleton(transLookup)
                .AddSingleton(A.Fake<IRConParser>())
                .AddSingleton(A.Fake<IParserRegexFactory>())
                .AddSingleton<DataFileLoader>()
                .AddSingleton(A.Fake<IGameLogReaderFactory>())
                .AddSingleton(A.Fake<IMetaService>())
                .AddSingleton(eventHandler)
                .AddSingleton(ConfigurationGenerators.CreateApplicationConfiguration())
                .AddSingleton(ConfigurationGenerators.CreateCommandConfiguration())
                .AddSingleton<IConfigurationHandler<ApplicationConfiguration>, ApplicationConfigurationHandlerMock>();

            return serviceCollection;
        }

        public static IServiceProvider SetupTestHooks(this IServiceProvider serviceProvider)
        {
            var mgr = serviceProvider.GetRequiredService<IManager>();
            A.CallTo(() => mgr.GetApplicationSettings())
                .Returns(serviceProvider.GetRequiredService<IConfigurationHandler<ApplicationConfiguration>>());

            return serviceProvider;
        }
    }
}
