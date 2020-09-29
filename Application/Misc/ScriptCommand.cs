using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using System;
using System.Threading.Tasks;
using static SharedLibraryCore.Database.Models.EFClient;

namespace IW4MAdmin.Application.Misc
{
    /// <summary>
    /// generic script command implementation
    /// </summary>
    public class ScriptCommand : Command
    {
        private readonly Action<GameEvent> _executeAction;

        public ScriptCommand(string name, string alias, string description, bool isTargetRequired, Permission permission,
            CommandArgument[] args, Action<GameEvent> executeAction, CommandConfiguration config, ITranslationLookup layout)
            : base(config, layout)
        {

            _executeAction = executeAction;
            Name = name;
            Alias = alias;
            Description = description;
            RequiresTarget = isTargetRequired;
            Permission = permission;
            Arguments = args;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            if (_executeAction == null)
            {
                throw new InvalidOperationException($"No execute action defined for command \"{Name}\"");
            }

            return Task.Run(() => _executeAction(E));
        }
    }
}
