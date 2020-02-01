using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Threading.Tasks;

namespace IW4ScriptCommands.Commands
{
    /// <summary>
    /// Example script command
    /// </summary>
    public class KillPlayerCommand : Command
    {
        public KillPlayerCommand(CommandConfiguration config, ITranslationLookup lookup) : base(config, lookup)
        {
            Name = "killplayer";
            Description = "kill a player";
            Alias = "kp";
            Permission = EFClient.Permission.Administrator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = "player",
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            var cmd = new ScriptCommand()
            {
                CommandName = "killplayer",
                ClientNumber = E.Target.ClientNumber,
                CommandArguments = new[] { E.Origin.ClientNumber.ToString() }
            };

            await cmd.Execute(E.Owner);
        }
    }
}
