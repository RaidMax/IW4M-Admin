using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Threading.Tasks;

namespace SharedLibraryCore.Commands
{
    /// <summary>
    /// Generates a token for use in webfront login
    /// </summary>
    public class RequestTokenCommand : Command
    {
        public RequestTokenCommand(CommandConfiguration config, ITranslationLookup lookup) : base(config, lookup)
        {
            Name = "requesttoken";
            Description = lookup["COMMANDS_GENERATETOKEN_DESC"];
            Alias = "rt";
            Permission = EFClient.Permission.Trusted;
            RequiresTarget = false;
        }

        public override Task ExecuteAsync(GameEvent E)
        {
            var state = E.Owner.Manager.TokenAuthenticator.GenerateNextToken(E.Origin.NetworkId);
            E.Origin.Tell(string.Format(_translationLookup["COMMANDS_GENERATETOKEN_SUCCESS"], state.Token, $"{state.RemainingTime} {_translationLookup["GLOBAL_MINUTES"]}", E.Origin.ClientId));

            return Task.CompletedTask;
        }
    }
}
