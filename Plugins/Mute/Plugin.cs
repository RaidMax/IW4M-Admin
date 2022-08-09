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
                await DataManager.ReadPersistentData(gameEvent.Origin);

                if (gameEvent.Origin.GetAdditionalProperty<bool>(MuteKey))
                {
                    await server.ExecuteCommandAsync($"muteClient {gameEvent.Origin.ClientNumber}");
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
