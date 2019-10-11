using SharedLibraryCore.Database.Models;
using System.Threading.Tasks;

namespace SharedLibraryCore.Commands
{
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
