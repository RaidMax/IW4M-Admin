using System;
using System.Linq;
using System.Threading.Tasks;
using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore
{
    public class CommandArgument
    {
        public string Name { get; set; }
        public bool Required { get; set; }
    }

    public abstract class Command
    {
        public Command(String commandName, String commandDescription, String commandAlias, EFClient.Permission requiredPermission, bool requiresTarget, CommandArgument[] param = null)
        { 
            Name = commandName;
            Description = commandDescription;
            Alias = commandAlias;
            Permission = requiredPermission;
            RequiresTarget = requiresTarget;
            Arguments = param ?? new CommandArgument[0];
        }

        //Execute the command
        abstract public Task ExecuteAsync(GameEvent E);

        public String Name { get; private set; }
        public String Description { get; private set; }
        public String Syntax => $"{Utilities.CurrentLocalization.LocalizationIndex["COMMAND_HELP_SYNTAX"]} !{Alias} {String.Join(" ", Arguments.Select(a => $"<{(a.Required ? "" : Utilities.CurrentLocalization.LocalizationIndex["COMMAND_HELP_OPTIONAL"] + " ")}{a.Name}>"))}";
        public String Alias { get; private set; }
        public int RequiredArgumentCount => Arguments.Count(c => c.Required);
        public bool RequiresTarget { get; private set; }
        public EFClient.Permission Permission { get; private set; }
        public CommandArgument[] Arguments { get; private set; }
    }
}
