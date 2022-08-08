using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace Mute;

public class Plugin : IPlugin
{
    public Plugin(IMetaServiceV2 metaService)
    {
        DataManager = new DataManager(metaService);
    }

    public string Name => "IW4x Mute";
    public float Version => 20220808f;
    public string Author => "Amos";

    public const string MuteKey = "IW4xMute";

    public static DataManager DataManager;

    public Task OnEventAsync(GameEvent gameEvent, Server server)
    {
        if (server.GameName != Server.Game.IW4) return Task.CompletedTask;
        
        switch (gameEvent.Type)
        {
            case GameEvent.EventType.Join:
                DataManager.ReadPersistentData(gameEvent.Origin);
                
                if (gameEvent.Origin.GetAdditionalProperty<bool>(MuteKey))
                {
                    server.ExecuteCommandAsync($"muteClient {gameEvent.Origin.ClientNumber}");
                }

                break;
        }

        return Task.CompletedTask;
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
