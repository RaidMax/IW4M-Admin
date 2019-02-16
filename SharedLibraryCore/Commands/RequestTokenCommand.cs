using SharedLibraryCore.Database.Models;
using System.Threading.Tasks;

namespace SharedLibraryCore.Commands
{
    public class RequestTokenCommand : Command
    {
        public RequestTokenCommand() :
            base("requesttoken", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_GENERATETOKEN_DESC"], "rt", EFClient.Permission.Trusted, false)
        { }

        public override Task ExecuteAsync(GameEvent E)
        {
            var state = E.Owner.Manager.TokenAuthenticator.GenerateNextToken(E.Origin.NetworkId);
            E.Origin.Tell(string.Format(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_GENERATETOKEN_SUCCESS"], state.Token, $"{state.RemainingTime} {Utilities.CurrentLocalization.LocalizationIndex["GLOBAL_MINUTES"]}", E.Origin.ClientId));

            return Task.CompletedTask;
        }
    }
}
