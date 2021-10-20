using System;
using Data.Abstractions;
using Data.Models.Server;
using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Factories
{
    /// <summary>
    /// implementation of IGameServerInstanceFactory
    /// </summary>
    internal class GameServerInstanceFactory : IGameServerInstanceFactory
    {
        private readonly ITranslationLookup _translationLookup;
        private readonly IMetaService _metaService;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// base constructor
        /// </summary>
        /// <param name="translationLookup"></param>
        /// <param name="rconConnectionFactory"></param>
        public GameServerInstanceFactory(ITranslationLookup translationLookup,
            IMetaService metaService,
            IServiceProvider serviceProvider)
        {
            _translationLookup = translationLookup;
            _metaService = metaService;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// creates an IW4MServer instance
        /// </summary>
        /// <param name="config">server configuration</param>
        /// <param name="manager">application manager</param>
        /// <returns></returns>
        public Server CreateServer(ServerConfiguration config, IManager manager)
        {
            return new IW4MServer(config,
                _serviceProvider.GetRequiredService<CommandConfiguration>(), _translationLookup, _metaService,
                _serviceProvider, _serviceProvider.GetRequiredService<IClientNoticeMessageFormatter>(),
                _serviceProvider.GetRequiredService<ILookupCache<EFServer>>());
        }
    }
}