using SharedLibraryCore.Database.Models;
using System.Threading.Tasks;

namespace SharedLibraryCore.Commands
{
    public class GenerateTokenCommand : Command
    {
        public GenerateTokenCommand() :
            base("generatetoken", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_GENERATETOKEN_DESC"], "gt", EFClient.Permission.Trusted, false)
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            string token = E.Owner.Manager.TokenAuthenticator.GenerateNextToken(E.Origin.NetworkId);
            var _event = token == null ?
                E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_GENERATETOKEN_FAIL"]) :
                E.Origin.Tell($"{Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_GENERATETOKEN_SUCCESS"]} {token}");

            return Task.CompletedTask;
        }
}
}
