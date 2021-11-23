using System.Collections.Generic;
using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands
{
    /// <summary>
    /// List all reports on the server
    /// </summary>
    public class ListReportsCommand : Command
    {
        public ListReportsCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "reports";
            Description = _translationLookup["COMMANDS_REPORTS_DESC"];
            Alias = "reps";
            Permission = EFClient.Permission.Moderator;
            RequiresTarget = false;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_CLEAR"],
                    Required = false
                }
            };
        }

        public override Task ExecuteAsync(GameEvent gameEvent)
        {
            if (gameEvent.Data != null && gameEvent.Data.ToLower().Contains(_translationLookup["COMMANDS_ARGS_CLEAR"]))
            {
                gameEvent.Owner.Reports = new List<Report>();
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_REPORTS_CLEAR_SUCCESS"]);
                return Task.CompletedTask;
            }

            if (gameEvent.Owner.Reports.Count < 1)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_REPORTS_NONE"]);
                return Task.CompletedTask;
            }

            foreach (var report in gameEvent.Owner.Reports)
            {
                gameEvent.Origin.Tell(
                    $"(Color::Accent){report.Origin.Name}(Color::White) -> (Color::Red){report.Target.Name}(Color::White): {report.Reason}");
            }

            return Task.CompletedTask;
        }
    }
}