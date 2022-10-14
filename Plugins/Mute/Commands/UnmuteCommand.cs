using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Mute.Commands;

public class UnmuteCommand : Command
{
    public UnmuteCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
        translationLookup)
    {
        Name = "unmute";
        Description = translationLookup["PLUGINS_MUTE_COMMANDS_UNMUTE_DESC"];
        Alias = "um";
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
            gameEvent.Origin.Tell(_translationLookup["PLUGINS_MUTE_SELF_TARGET"]);
            return;
        }

        if (await Plugin.MuteManager.Unmute(gameEvent.Owner, gameEvent.Origin, gameEvent.Target, gameEvent.Data))
        {
            gameEvent.Origin.Tell(_translationLookup["PLUGINS_MUTE_COMMANDS_UNMUTE_UNMUTED"]
                .FormatExt(gameEvent.Target.CleanedName));
            gameEvent.Target.Tell(_translationLookup["PLUGINS_MUTE_COMMANDS_UNMUTE_TARGET_UNMUTED"]
                .FormatExt(gameEvent.Data));
            return;
        }

        gameEvent.Origin.Tell(_translationLookup["PLUGINS_MUTE_COMMANDS_UNMUTE_NOT_MUTED"]
            .FormatExt(gameEvent.Target.CleanedName));
    }
}
