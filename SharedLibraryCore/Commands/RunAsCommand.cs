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

        public override async Task ExecuteAsync(GameEvent E)
        {
            if (E.IsTargetingSelf())
            {
                E.Origin.Tell(_translationLookup["COMMANDS_RUN_AS_SELF"]);
                return;
            }

            if (!E.CanPerformActionOnTarget())
            {
                E.Origin.Tell(_translationLookup["COMMANDS_RUN_AS_FAIL_PERM"]);
                return;
            }

            string cmd = $"{Utilities.CommandPrefix}{E.Data}";
            var impersonatedCommandEvent = new GameEvent()
            {
                Type = GameEvent.EventType.Command,
                Origin = E.Target,
                ImpersonationOrigin = E.Origin,
                Message = cmd,
                Data = cmd,
                Owner = E.Owner
            };
            E.Owner.Manager.GetEventHandler().AddEvent(impersonatedCommandEvent);

            var result = await impersonatedCommandEvent.WaitAsync(Utilities.DefaultCommandTimeout, E.Owner.Manager.CancellationToken);
            var response = E.Owner.CommandResult.Where(c => c.ClientId == E.Target.ClientId).ToList();

            // remove the added command response
            for (int i = 0; i < response.Count; i++)
            {
                E.Origin.Tell(_translationLookup["COMMANDS_RUN_AS_SUCCESS"].FormatExt(response[i].Response));
                E.Owner.CommandResult.Remove(response[i]);
            }
        }
    }
}
