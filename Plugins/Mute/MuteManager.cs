using System.Resources;
using SharedLibraryCore;

namespace Mute;

public class MuteManager
{
    public async Task<bool> Mute(GameEvent gameEvent)
    {
        if (gameEvent.Target.IsIngame)
        {
            if (gameEvent.Target.GetAdditionalProperty<MuteState>(Plugin.MuteKey) == MuteState.Muted)
            {
                await gameEvent.Owner.ExecuteCommandAsync($"unmute {gameEvent.Target.ClientNumber}");
                await Plugin.DataManager.WritePersistentData(gameEvent.Target, MuteState.Unmuted);
                return false;
            }

            await gameEvent.Owner.ExecuteCommandAsync($"muteClient {gameEvent.Target.ClientNumber}");
            await Plugin.DataManager.WritePersistentData(gameEvent.Target, MuteState.Muted);
            return true;
        }

        if (await Plugin.DataManager.ReadPersistentData(gameEvent.Target) == MuteState.Muted)
        {
            await Plugin.DataManager.WritePersistentData(gameEvent.Target, MuteState.Unmuting);
            return false;
        }

        await Plugin.DataManager.WritePersistentData(gameEvent.Target, MuteState.Muted);
        return true;
    }
}
