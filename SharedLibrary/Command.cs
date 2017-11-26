using System;
using System.Linq;
using System.Threading.Tasks;

using SharedLibrary.Objects;

namespace SharedLibrary
{
    public class CommandArgument
    {
        public string Name { get; set; }
        public bool Required { get; set; }
    }

    public abstract class Command
    {
        public Command(String commandName, String commandDescription, String commandAlias, Player.Permission requiredPermission, bool requiresTarget, CommandArgument[] param = null)
        { 
            Name = commandName;
            Description = commandDescription;
            Alias = commandAlias;
            Permission = requiredPermission;
            RequiresTarget = requiresTarget;
            Arguments = param ?? new CommandArgument[0];
        }

        //Execute the command
        abstract public Task ExecuteAsync(Event E);

        public String Name { get; private set; }
        public String Description { get; private set; }
        public String Syntax => $"syntax: !{Alias} {String.Join(" ", Arguments.Select(a => $"<{(a.Required ? "" : "optional ")}{a.Name}>"))}";
        public String Alias { get; private set; }
        public int RequiredArgumentCount => Arguments.Count(c => c.Required);
        public bool RequiresTarget { get; private set; }
        public Player.Permission Permission { get; private set; }
        public CommandArgument[] Arguments { get; private set; }
    }
}
