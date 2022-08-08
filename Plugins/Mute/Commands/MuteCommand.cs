using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace Mute.Commands;

public class MuteCommand : Command
{
    public MuteCommand(CommandConfiguration config, ITranslationLookup layout) : base(config, layout)
    {
        Name = "mute";
        Description = "Check your winnings!";
        Alias = "mu";
        Permission = EFClient.Permission.Moderator;
        RequiresTarget = true;
        Arguments = new[]
        {
            new CommandArgument
            {
                Name = "Player",
                Required = true
            }
        };
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        var muteManager = new MuteManager();

        if (muteManager.Mute(gameEvent))
        {
            gameEvent.Origin.Tell($"Muted {gameEvent.Target.Name}");
            return Task.CompletedTask;
        }
        
        gameEvent.Origin.Tell($"Unmuted {gameEvent.Target.Name}");
        return Task.CompletedTask;
    }
}
