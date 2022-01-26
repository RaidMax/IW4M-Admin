using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Commands
{
    /// <summary>
    ///     Provides a way for administrators to "unlink" linked accounts
    ///     This problem is common in IW4x where the client identifier is a file
    ///     that is commonly transmitted when uploading and sharing the game files
    ///     This command creates a new link and assigns the guid, and all aliases with the current IP
    ///     associated to the provided client ID to the new link
    /// </summary>
    public class UnlinkClientCommand : Command
    {
        public UnlinkClientCommand(CommandConfiguration config, ITranslationLookup lookup) : base(config, lookup)
        {
            Name = "unlinkclient";
            Description = lookup["COMMANDS_UNLINK_CLIENT_DESC"];
            Alias = "uc";
            Permission = EFClient.Permission.Administrator;
            RequiresTarget = true;
        }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Owner.Manager.GetClientService().UnlinkClient(E.Target.ClientId);
            E.Origin.Tell(_translationLookup["COMMANDS_UNLINK_CLIENT_SUCCESS"].FormatExt(E.Target));
        }
    }
}