using IW4MAdmin.Application.IO;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore.Interfaces;
using System;
using Microsoft.Extensions.Logging;

namespace IW4MAdmin.Application.Factories
{
    public class GameLogReaderFactory : IGameLogReaderFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public GameLogReaderFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public IGameLogReader CreateGameLogReader(Uri[] logUris, IEventParser eventParser)
        {
            var baseUri = logUris[0];
            if (baseUri.Scheme == Uri.UriSchemeHttp)
            {
                return new GameLogReaderHttp(logUris, eventParser, _serviceProvider.GetRequiredService<ILogger<GameLogReaderHttp>>());
            }

            else if (baseUri.Scheme == Uri.UriSchemeFile)
            {
                return new GameLogReader(baseUri.LocalPath, eventParser, _serviceProvider.GetRequiredService<ILogger<GameLogReader>>());
            }

            throw new NotImplementedException($"No log reader implemented for Uri scheme \"{baseUri.Scheme}\"");
        }
    }
}
