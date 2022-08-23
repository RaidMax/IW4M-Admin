using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace Mute;

public class Plugin : IPlugin
{
    public Plugin(IMetaServiceV2 metaService)
    {
        DataManager = new DataManager(metaService);
    }

    public string Name => "Mute";
    public float Version => (float) Utilities.GetVersionAsDouble();
    public string Author => "Amos";

    public static string MuteKey = "IW4MMute";

    public static DataManager DataManager;
    public static readonly Server.Game[] SupportedGames = {Server.Game.IW4};

    public async Task OnEventAsync(GameEvent gameEvent, Server server)
    {
        if (!SupportedGames.Contains(server.GameName)) return;

        switch (gameEvent.Type)
        {
            case GameEvent.EventType.Join:
                switch (await DataManager.ReadPersistentData(gameEvent.Origin))
                {
                    case MuteState.Muted:
                        await server.ExecuteCommandAsync($"muteClient {gameEvent.Origin.ClientNumber}");
                        break;
                    case MuteState.Unmuting:
                        await server.ExecuteCommandAsync($"unmute {gameEvent.Origin.ClientNumber}");
                        await DataManager.WritePersistentData(gameEvent.Origin, MuteState.Unmuted);
                        break;
                    case MuteState.Unmuted:
                        break;
                }

                break;
        }
    }

    public Task OnLoadAsync(IManager manager)
    {
        return Task.CompletedTask;
    }

    public Task OnUnloadAsync()
    {
        return Task.CompletedTask;
    }

    public Task OnTickAsync(Server server)
    {
        return Task.CompletedTask;
    }
}
