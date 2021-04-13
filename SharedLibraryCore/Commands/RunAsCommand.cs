using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;
using System.Linq;
using System.Threading.Tasks;

namespace SharedLibraryCore.Commands
{
    public class RunAsCommand : Command
    {
        public RunAsCommand(CommandConfiguration config, ITranslationLookup lookup) : base(config, lookup)
        {
            Name = "runas";
            Description = lookup["COMMANDS_RUN_AS_DESC"];
            Alias = "ra";
            Permission = EFClient.Permission.Moderator;
            RequiresTarget = true;
            Arguments = new[]
            {
                new CommandArgument()
                {
                    Name = lookup["COMMANDS_ARGS_COMMANDS"],
                    Required = true
                }
            };
        }

        public override async Task ExecuteAsync(GameEvent gameEvent)
        {
            if (gameEvent.IsTargetingSelf())
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_RUN_AS_SELF"]);
                return;
            }

            if (!gameEvent.CanPerformActionOnTarget())
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_RUN_AS_FAIL_PERM"]);
                return;
            }

            var cmd = $"{Utilities.CommandPrefix}{gameEvent.Data}";
            var impersonatedCommandEvent = new GameEvent()
            {
                Type = GameEvent.EventType.Command,
                Origin = gameEvent.Target,
                ImpersonationOrigin = gameEvent.Origin,
                Message = cmd,
                Data = cmd,
                Owner = gameEvent.Owner,
                CorrelationId = gameEvent.CorrelationId
            };
            gameEvent.Owner.Manager.AddEvent(impersonatedCommandEvent);

            var result = await impersonatedCommandEvent.WaitAsync(Utilities.DefaultCommandTimeout, gameEvent.Owner.Manager.CancellationToken);

            // remove the added command response
            foreach (var output in result.Output)
            {
                gameEvent.Origin.Tell(_translationLookup["COMMANDS_RUN_AS_SUCCESS"].FormatExt(output));
            }
        }
    }
}
