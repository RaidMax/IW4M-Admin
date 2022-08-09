using SharedLibraryCore;

namespace Mute;

public class MuteManager
{
    public async Task<bool> Mute(GameEvent gameEvent)
    {
        if (gameEvent.Target.GetAdditionalProperty<bool>(Plugin.MuteKey))
        {
            await gameEvent.Owner.ExecuteCommandAsync($"unmute {gameEvent.Target.ClientNumber}");
            await Plugin.DataManager!.WritePersistentData(gameEvent.Target, false);
            return false;
        }

        await gameEvent.Owner.ExecuteCommandAsync($"muteClient {gameEvent.Target.ClientNumber}");
        await Plugin.DataManager!.WritePersistentData(gameEvent.Target, true);
        return true;
    }
}
