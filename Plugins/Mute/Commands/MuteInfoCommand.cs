using Data.Models.Client;
using Humanizer;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.Mute.Commands;

public class MuteInfoCommand : Command
{
    private readonly MuteManager _muteManager;

    public MuteInfoCommand(CommandConfiguration config, ITranslationLookup translationLookup, MuteManager muteManager,
        MuteConfiguration muteConfiguration) : base(config, translationLookup)
    {
        _muteManager = muteManager;
        Name = "muteinfo";
        Description = translationLookup["PLUGINS_MUTE_COMMANDS_MUTEINFO_DESC"];
        Alias = "mi";
        Permission = EFClient.Permission.Moderator;
        RequiresTarget = true;
        SupportedGames = muteConfiguration.GameCommands.Select(x => x.GameName).ToArray();
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = translationLookup["COMMANDS_ARGS_PLAYER"],
                Required = true
            }
        };
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        var currentMuteMeta = await _muteManager.GetCurrentMuteState(gameEvent.Target);
        switch (currentMuteMeta.MuteState)
        {
            case MuteState.Muted when currentMuteMeta.Expiration is null:
                gameEvent.Origin.Tell(_translationLookup["PLUGINS_MUTE_COMMANDS_MUTEINFO_SUCCESS"]
                    .FormatExt(gameEvent.Target.Name, currentMuteMeta.Reason));
                return;
            case MuteState.Muted when currentMuteMeta.Expiration.HasValue && currentMuteMeta.Expiration.Value > DateTime.UtcNow:
                var remainingTime = (currentMuteMeta.Expiration.Value - DateTime.UtcNow).HumanizeForCurrentCulture();
                gameEvent.Origin.Tell(_translationLookup["PLUGINS_MUTE_COMMANDS_MUTEINFO_TM_SUCCESS"]
                    .FormatExt(gameEvent.Target.Name, currentMuteMeta.Reason, remainingTime));
                return;
            default:
                gameEvent.Origin.Tell(_translationLookup["PLUGINS_MUTE_COMMANDS_MUTEINFO_NONE"]);
                break;
        }
    }
}
