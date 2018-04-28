using SharedLibraryCore;
using SharedLibraryCore.Objects;
using System.Threading.Tasks;

namespace IW4ScriptCommands.Commands
{
    class Balance : Command
    {
        public Balance() : base("balance", "balance teams", "bal", Player.Permission.Trusted, false, null)
        {
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Owner.ExecuteCommandAsync("sv_iw4madmin_command balance");
            await E.Origin.Tell("Balance command sent");
        }
    }
}
