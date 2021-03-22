using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Threading.Tasks;
using Data.Models.Client;
using Microsoft.Extensions.Logging;
using static SharedLibraryCore.Database.Models.EFClient;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace IW4MAdmin.Application.Misc
{
    /// <summary>
    /// generic script command implementation
    /// </summary>
    public class ScriptCommand : Command
    {
        private readonly Action<GameEvent> _executeAction;
        private readonly ILogger _logger;

        public ScriptCommand(string name, string alias, string description, bool isTargetRequired, EFClient.Permission permission,
            CommandArgument[] args, Action<GameEvent> executeAction, CommandConfiguration config, ITranslationLookup layout, ILogger<ScriptCommand> logger)
            : base(config, layout)
        {

            _executeAction = executeAction;
            _logger = logger;
            Name = name;
            Alias = alias;
            Description = description;
            RequiresTarget = isTargetRequired;
            Permission = permission;
            Arguments = args;
        }

        public override async Task ExecuteAsync(GameEvent e)
        {
            if (_executeAction == null)
            {
                throw new InvalidOperationException($"No execute action defined for command \"{Name}\"");
            }

            try
            {
                await Task.Run(() => _executeAction(e));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute ScriptCommand action for command {command} {@event}", Name, e);
            }
        }
    }
}
