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
            }
        };
    }

    private readonly MuteManager _muteManager = new();

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (await _muteManager.Mute(gameEvent))
        {
            gameEvent.Origin.Tell($"{_translationLookup["PLUGINS_MUTE_MUTED"]} {gameEvent.Target.Name}");
            return;
        }

        gameEvent.Origin.Tell($"{_translationLookup["PLUGINS_MUTE_UNMUTED"]} {gameEvent.Target.Name}");
    }
}
