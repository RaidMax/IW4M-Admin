using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Commands
{
    /// <summary>
    ///     Generates a token for use in webfront login
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

        public override Task ExecuteAsync(GameEvent gameEvent)
        {
            var state = gameEvent.Owner.Manager.TokenAuthenticator.GenerateNextToken(new TokenIdentifier
            {
                Game = gameEvent.Origin.GameName,
                NetworkId = gameEvent.Origin.NetworkId
            });
            gameEvent.Origin.Tell(string.Format(_translationLookup["COMMANDS_GENERATETOKEN_SUCCESS"], state.Token,
                $"{state.RemainingTime} {_translationLookup["GLOBAL_MINUTES"]}", gameEvent.Origin.ClientId));

            return Task.CompletedTask;
        }
    }
}
