﻿using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System.Collections;

namespace IW4MAdmin.Application.Factories
{
    /// <summary>
    /// implementation of IGameServerInstanceFactory
    /// </summary>
    internal class GameServerInstanceFactory : IGameServerInstanceFactory
    {
        private readonly ITranslationLookup _translationLookup;
        private readonly IRConConnectionFactory _rconConnectionFactory;
        private readonly IGameLogReaderFactory _gameLogReaderFactory;
        private readonly IMetaService _metaService;

        /// <summary>
        /// base constructor
        /// </summary>
        /// <param name="translationLookup"></param>
        /// <param name="rconConnectionFactory"></param>
        public GameServerInstanceFactory(ITranslationLookup translationLookup, IRConConnectionFactory rconConnectionFactory, IGameLogReaderFactory gameLogReaderFactory, IMetaService metaService)
        {
            _translationLookup = translationLookup;
            _rconConnectionFactory = rconConnectionFactory;
            _gameLogReaderFactory = gameLogReaderFactory;
            _metaService = metaService;
        }

        /// <summary>
        /// creates an IW4MServer instance
        /// </summary>
        /// <param name="config">server configuration</param>
        /// <param name="manager">application manager</param>
        /// <returns></returns>
        public Server CreateServer(ServerConfiguration config, IManager manager)
        {
            return new IW4MServer(manager, config, _translationLookup, _rconConnectionFactory, _gameLogReaderFactory, _metaService);
        }
    }
}
