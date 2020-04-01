using SharedLibraryCore.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace IW4MAdmin.Application.Factories
{
    /// <summary>
    /// Implementation of the IParserRegexFactory
    /// </summary>
    public class ParserRegexFactory : IParserRegexFactory
    {
        private readonly IServiceProvider _serviceProvider;

        /// <inheritdoc/>
        public ParserRegexFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public ParserRegex CreateParserRegex()
        {
            return new ParserRegex(_serviceProvider.GetService<IParserPatternMatcher>());
        }
    }
}
