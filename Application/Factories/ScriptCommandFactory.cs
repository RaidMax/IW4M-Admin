using IW4MAdmin.Application.Misc;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using static SharedLibraryCore.Database.Models.EFClient;

namespace IW4MAdmin.Application.Factories
{
    /// <summary>
    /// implementation of IScriptCommandFactory
    /// </summary>
    public class ScriptCommandFactory : IScriptCommandFactory
    {
        private CommandConfiguration _config;
        private readonly ITranslationLookup _transLookup;

        public ScriptCommandFactory(CommandConfiguration  config, ITranslationLookup transLookup)
        {
            _config = config;
            _transLookup = transLookup;
        }

        /// <inheritdoc/>
        public IManagerCommand CreateScriptCommand(string name, string alias, string description, string permission, bool isTargetRequired, IEnumerable<(string, bool)> args, Action<GameEvent> executeAction)
        {
            var permissionEnum = Enum.Parse<Permission>(permission);
            var argsArray = args.Select(_arg => new CommandArgument
            {
                Name = _arg.Item1,
                Required = _arg.Item2
            }).ToArray();

            return new ScriptCommand(name, alias, description, isTargetRequired, permissionEnum, argsArray, executeAction, _config, _transLookup);
        }
    }
}
