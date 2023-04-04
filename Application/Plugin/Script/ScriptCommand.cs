using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Models;
using Data.Models.Client;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Plugin.Script
{
    /// <summary>
    /// generic script command implementation
    /// </summary>
    public class ScriptCommand : Command
    {
        private readonly Func<GameEvent, Task> _executeAction;
        private readonly ILogger _logger;

        public ScriptCommand(string name, string alias, string description, bool isTargetRequired,
            EFClient.Permission permission,
            IEnumerable<CommandArgument> args, Func<GameEvent, Task> executeAction, CommandConfiguration config,
            ITranslationLookup layout, ILogger<ScriptCommand> logger, IEnumerable<Reference.Game> supportedGames)
            : base(config, layout)
        {
            _executeAction = executeAction;
            _logger = logger;
            Name = name;
            Alias = alias;
            Description = description;
            RequiresTarget = isTargetRequired;
            Permission = permission;
            Arguments = args.ToArray();
            SupportedGames = supportedGames?.Select(game => (Server.Game)game).ToArray();
        }

        public override async Task ExecuteAsync(GameEvent e)
        {
            if (_executeAction == null)
            {
                throw new InvalidOperationException($"No execute action defined for command \"{Name}\"");
            }

            try
            {
                await _executeAction(e);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute ScriptCommand action for command {Command} {@Event}", Name, e);
            }
        }
    }
}
