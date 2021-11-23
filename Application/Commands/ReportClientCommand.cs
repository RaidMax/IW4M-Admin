using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Commands
{
    /// <summary>
    /// Report client for given reason
    /// </summary>
    public class ReportClientCommand : Command
    {
        public ReportClientCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
            translationLookup)
        {
            Name = "report";
            Description = _translationLookup["COMMANDS_REPORT_DESC"];
            Alias = "rep";
            Permission = EFClient.Permission.User;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_PLAYER"],
                    Required = true
                },
                new CommandArgument
                {
                    Name = _translationLookup["COMMANDS_ARGS_REASON"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent commandEvent)
        {
            if (commandEvent.Data.ToLower().Contains("camp"))
            {
                commandEvent.Origin.Tell(_translationLookup["COMMANDS_REPORT_FAIL_CAMP"]);
                return;
            }

            var success = false;

            switch ((await commandEvent.Target.Report(commandEvent.Data, commandEvent.Origin)
                .WaitAsync(Utilities.DefaultCommandTimeout, commandEvent.Owner.Manager.CancellationToken)).FailReason)
            {
                case GameEvent.EventFailReason.None:
                    commandEvent.Origin.Tell(_translationLookup["COMMANDS_REPORT_SUCCESS"]);
                    success = true;
                    break;
                case GameEvent.EventFailReason.Exception:
                    commandEvent.Origin.Tell(_translationLookup["COMMANDS_REPORT_FAIL_DUPLICATE"]);
                    break;
                case GameEvent.EventFailReason.Permission:
                    commandEvent.Origin.Tell(_translationLookup["COMMANDS_REPORT_FAIL"]
                        .FormatExt(commandEvent.Target.Name));
                    break;
                case GameEvent.EventFailReason.Invalid:
                    commandEvent.Origin.Tell(_translationLookup["COMMANDS_REPORT_FAIL_SELF"]);
                    break;
                case GameEvent.EventFailReason.Throttle:
                    commandEvent.Origin.Tell(_translationLookup["COMMANDS_REPORT_FAIL_TOOMANY"]);
                    break;
            }

            if (success)
            {
                commandEvent.Owner.ToAdmins(
                    $"(Color::Accent){commandEvent.Origin.Name}(Color::White) -> (Color::Red){commandEvent.Target.Name}(Color::White): {commandEvent.Data}");
            }
        }
    }
}