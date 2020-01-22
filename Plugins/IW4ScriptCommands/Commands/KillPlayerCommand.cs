using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using System.Threading.Tasks;

namespace IW4ScriptCommands.Commands
{
    /// <summary>
    /// Example script command
    /// </summary>
    public class KillPlayerCommand : Command
    {
        public KillPlayerCommand() : base("killplayer", "kill a player", "kp", EFClient.Permission.Administrator, true, new[]
        {
            new CommandArgument()
            {
                Name = "player",
                Required = true
            }
        })
        { }

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
