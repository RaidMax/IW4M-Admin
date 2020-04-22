using ApplicationTests.Fixtures;
using FakeItEasy;
using IW4MAdmin;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Services;

namespace ApplicationTests
{
    static class DepedencyInjectionExtensions
    {
        public static IServiceCollection BuildBase(this IServiceCollection serviceCollection)
        {
            var manager = A.Fake<IManager>();
            var logger = A.Fake<ILogger>();
            A.CallTo(() => manager.GetLogger(A<long>.Ignored))
                .Returns(logger);

            serviceCollection.AddSingleton(logger)
                .AddSingleton(manager)
                .AddSingleton(A.Fake<IRConConnectionFactory>())
                .AddSingleton(A.Fake<IRConConnection>())
                .AddSingleton(A.Fake<ITranslationLookup>())
                .AddSingleton(A.Fake<IRConParser>())
                .AddSingleton(A.Fake<IParserRegexFactory>())
                .AddSingleton(A.Fake<ClientService>());

            serviceCollection.AddSingleton(_sp => new IW4MServer(_sp.GetRequiredService<IManager>(), ConfigurationGenerators.CreateServerConfiguration(),
                _sp.GetRequiredService<ITranslationLookup>(), _sp.GetRequiredService<IRConConnectionFactory>())
            {
                RconParser = _sp.GetRequiredService<IRConParser>()
            });

            return serviceCollection;
        }
    }
}
