using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Mute.Commands;

public class MuteCommand : Command
{
    public MuteCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
        translationLookup)
    {
        Name = "mute";
        Description = translationLookup["PLUGINS_MUTE_COMMANDS_MUTE_DESC"];
        Alias = "mu";
        Permission = EFClient.Permission.Moderator;
        RequiresTarget = true;
        SupportedGames = Plugin.SupportedGames;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = translationLookup["COMMANDS_ARGS_PLAYER"],
                Required = true
            },
            new CommandArgument
            {
                Name = translationLookup["COMMANDS_ARGS_REASON"],
                Required = true
            }
        };
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (gameEvent.Origin.ClientId == gameEvent.Target.ClientId)
        {
            gameEvent.Origin.Tell(_translationLookup["COMMANDS_DENY_SELF_TARGET"]);
            return;
        }

        if (await Plugin.MuteManager.Mute(gameEvent.Owner, gameEvent.Origin, gameEvent.Target, null, gameEvent.Data))
        {
            gameEvent.Origin.Tell(_translationLookup["PLUGINS_MUTE_COMMANDS_MUTE_MUTED"]
                .FormatExt(gameEvent.Target.CleanedName));
            gameEvent.Target.Tell(_translationLookup["PLUGINS_MUTE_COMMANDS_MUTE_TARGET_MUTED"]
                .FormatExt(gameEvent.Data));
            return;
        }

        gameEvent.Origin.Tell(_translationLookup["PLUGINS_MUTE_COMMANDS_MUTE_NOT_UNMUTED"]
            .FormatExt(gameEvent.Target.CleanedName));
    }
}
