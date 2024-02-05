using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands;

public class ClearAllReportsCommand : Command
{
    public ClearAllReportsCommand(CommandConfiguration config, ITranslationLookup layout) : base(config, layout)
    {
        Name = "clearallreports";
        Description = _translationLookup["COMMANDS_REPORTS_CLEAR_DESC"];
        Alias = "car";
        Permission = EFClient.Permission.Administrator;
        RequiresTarget = false;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        foreach (var server in gameEvent.Owner.Manager.GetServers())
        {
            server.Reports.Clear();
        }

        gameEvent.Origin.Tell(_translationLookup["COMMANDS_REPORTS_CLEAR_SUCCESS"]);

        return Task.CompletedTask;
    }
}
