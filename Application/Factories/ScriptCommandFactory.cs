using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Data.Models;
using Data.Models.Client;
using IW4MAdmin.Application.Plugin.Script;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IW4MAdmin.Application.Factories
{
    /// <summary>
    /// implementation of IScriptCommandFactory
    /// </summary>
    public class ScriptCommandFactory : IScriptCommandFactory
    {
        private readonly CommandConfiguration _config;
        private readonly ITranslationLookup _transLookup;
        private readonly IServiceProvider _serviceProvider;

        public ScriptCommandFactory(CommandConfiguration  config, ITranslationLookup transLookup, IServiceProvider serviceProvider)
        {
            _config = config;
            _transLookup = transLookup;
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public IManagerCommand CreateScriptCommand(string name, string alias, string description, string permission, 
            bool isTargetRequired, IEnumerable<CommandArgument> args, Func<GameEvent, Task> executeAction, IEnumerable<Reference.Game> supportedGames)
        {
            var permissionEnum = Enum.Parse<EFClient.Permission>(permission);

            return new ScriptCommand(name, alias, description, isTargetRequired, permissionEnum, args, executeAction,
                _config, _transLookup, _serviceProvider.GetRequiredService<ILogger<ScriptCommand>>(), supportedGames);
        }
    }
}
