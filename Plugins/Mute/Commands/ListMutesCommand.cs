using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Mute.Commands;

public class ListMutesCommand : Command
{
    public ListMutesCommand(CommandConfiguration config, ITranslationLookup translationLookup) : base(config,
        translationLookup)
    {
        Name = "listmutes";
        Description = translationLookup["PLUGINS_MUTE_COMMANDS_LISTMUTES_DESC"];
        Alias = "lm";
        Permission = EFClient.Permission.Moderator;
        RequiresTarget = false;
        SupportedGames = Plugin.SupportedGames;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (!Plugin.MuteManager.HasMutes)
        {
            gameEvent.Origin.Tell(_translationLookup["PLUGINS_MUTE_COMMANDS_LISTMUTES_EMPTY"]);
            return;
        }

        var output = Plugin.MuteManager.Mutes
            .Select((mute, index) =>
                _translationLookup["PLUGINS_MUTE_COMMANDS_LISTMUTES_FORMAT"].FormatExt(index + 1, mute.ClientId,
                    mute.CleanedName, mute.Expiration is not null
                        ? mute.Expiration.Value.HumanizeForCurrentCulture()
                        : _translationLookup["WEBFRONT_ACTION_SELECTION_PERMANENT"], mute.AdminId, mute.Reason));

        gameEvent.Origin.Tell(_translationLookup["PLUGINS_MUTE_COMMANDS_LISTMUTES"]
            .FormatExt(Plugin.MuteManager.Mutes.Count()));
        await gameEvent.Origin.TellAsync(output);
    }
}
