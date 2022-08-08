using SharedLibraryCore;

namespace Mute;

public class MuteManager
{
    public bool Mute(GameEvent gameEvent)
    {
        if (gameEvent.Target.GetAdditionalProperty<bool>(Plugin.MuteKey))
        {
            gameEvent.Owner.ExecuteCommandAsync($"unmute {gameEvent.Target.ClientNumber}");
            Plugin.DataManager.WritePersistentData(gameEvent.Target, false);
            return false;
        }

        gameEvent.Owner.ExecuteCommandAsync($"muteClient {gameEvent.Target.ClientNumber}");
        Plugin.DataManager.WritePersistentData(gameEvent.Target, true);
        return true;
    }
}
