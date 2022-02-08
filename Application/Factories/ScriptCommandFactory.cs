using IW4MAdmin.Application.Misc;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Models.Client;
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
            bool isTargetRequired, IEnumerable<(string, bool)> args, Func<GameEvent, Task> executeAction, Server.Game[] supportedGames)
        {
            var permissionEnum = Enum.Parse<EFClient.Permission>(permission);
            var argsArray = args.Select(_arg => new CommandArgument
            {
                Name = _arg.Item1,
                Required = _arg.Item2
            }).ToArray();

            return new ScriptCommand(name, alias, description, isTargetRequired, permissionEnum, argsArray, executeAction,
                _config, _transLookup, _serviceProvider.GetRequiredService<ILogger<ScriptCommand>>(), supportedGames);
        }
    }
}
