using ApplicationTests.Fixtures;
using ApplicationTests.Mocks;
using FakeItEasy;
using IW4MAdmin;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;
using System;

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
            var logger = A.Fake<ILogger>();

            A.CallTo(() => manager.GetLogger(A<long>.Ignored))
                .Returns(logger);

            serviceCollection.AddSingleton(logger)
                .AddSingleton(manager)
                .AddSingleton<IDatabaseContextFactory, DatabaseContextFactoryMock>()
                .AddSingleton(A.Fake<IRConConnectionFactory>())
                .AddSingleton(A.Fake<IRConConnection>())
                .AddSingleton(A.Fake<ITranslationLookup>())
                .AddSingleton(A.Fake<IRConParser>())
                .AddSingleton(A.Fake<IParserRegexFactory>())
                .AddSingleton<DataFileLoader>()
                .AddSingleton(A.Fake<ClientService>())
                .AddSingleton(A.Fake<IGameLogReaderFactory>())
                .AddSingleton(eventHandler)
                .AddSingleton(ConfigurationGenerators.CreateApplicationConfiguration())
                .AddSingleton(ConfigurationGenerators.CreateCommandConfiguration())
                .AddSingleton<IConfigurationHandler<ApplicationConfiguration>, ApplicationConfigurationHandlerMock>();

            serviceCollection.AddSingleton(_sp => new IW4MServer(_sp.GetRequiredService<IManager>(), ConfigurationGenerators.CreateServerConfiguration(),
                _sp.GetRequiredService<ITranslationLookup>(), _sp.GetRequiredService<IRConConnectionFactory>(), _sp.GetRequiredService<IGameLogReaderFactory>())
            {
                RconParser = _sp.GetRequiredService<IRConParser>()
            });

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
