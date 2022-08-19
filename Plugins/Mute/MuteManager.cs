using SharedLibraryCore;

namespace Mute;

public class MuteManager
{
    public async Task<bool> Mute(GameEvent gameEvent)
    {
        if (await Plugin.DataManager.ReadPersistentData(gameEvent.Target) == MuteState.Muted)
        {
            await gameEvent.Owner.ExecuteCommandAsync($"unmute {gameEvent.Target.ClientNumber}");
            await Plugin.DataManager.WritePersistentData(gameEvent.Target,
                gameEvent.Target.IsIngame ? MuteState.Unmuted : MuteState.Unmuting);
            return false;
        }

        await gameEvent.Owner.ExecuteCommandAsync($"muteClient {gameEvent.Target.ClientNumber}");
        await Plugin.DataManager.WritePersistentData(gameEvent.Target, MuteState.Muted);
        return true;
    }
}
