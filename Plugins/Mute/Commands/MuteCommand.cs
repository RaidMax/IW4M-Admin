using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.Mute.Commands;

public class MuteCommand : Command
{
    private readonly MuteManager _muteManager;

    public MuteCommand(CommandConfiguration config, ITranslationLookup translationLookup, MuteManager muteManager,
        MuteConfiguration muteConfiguration) : base(config, translationLookup)
    {
        _muteManager = muteManager;
        Name = "mute";
        Description = translationLookup["PLUGINS_MUTE_COMMANDS_MUTE_DESC"];
        Alias = "mu";
        Permission = EFClient.Permission.Moderator;
        RequiresTarget = true;
        SupportedGames = muteConfiguration.GameCommands.Select(x => x.GameName).ToArray();
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

        if (await _muteManager.Mute(gameEvent.Owner, gameEvent.Origin, gameEvent.Target, null, gameEvent.Data))
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
