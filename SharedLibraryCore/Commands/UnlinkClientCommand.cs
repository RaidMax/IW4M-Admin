using SharedLibraryCore.Database.Models;
using System.Threading.Tasks;

namespace SharedLibraryCore.Commands
{
    /// <summary>
    /// Provides a way for administrators to "unlink" linked accounts
    /// This problem is common in IW4x where the client identifier is a file
    /// that is commonly transmitted when uploading and sharing the game files
    /// This command creates a new link and assigns the guid, and all aliases with the current IP
    /// associated to the provided client ID to the new link
    /// </summary>
    public class UnlinkClientCommand : Command
    {
        public UnlinkClientCommand() :
            base("unlinkclient", Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNLINK_CLIENT_DESC"], "uc", EFClient.Permission.Administrator, true)
        { }

        public override async Task ExecuteAsync(GameEvent E)
        {
            await E.Owner.Manager.GetClientService().UnlinkClient(E.Target.ClientId);
            E.Origin.Tell(Utilities.CurrentLocalization.LocalizationIndex["COMMANDS_UNLINK_CLIENT_SUCCESS"].FormatExt(E.Target));
        }
    }
}
