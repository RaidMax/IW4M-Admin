using Data.Models.Client;
using SharedLibraryCore.Interfaces;

namespace Mute;

public class DataManager
{
    public DataManager(IMetaServiceV2 metaService)
    {
        _metaService = metaService;
    }

    private readonly IMetaServiceV2 _metaService;

    public async void ReadPersistentData(EFClient client) => client.SetAdditionalProperty(Plugin.MuteKey,
        bool.Parse((await _metaService.GetPersistentMeta(Plugin.MuteKey, client.ClientId))?.Value ?? "false"));

    public async void WritePersistentData(EFClient client, bool state)
    {
        await _metaService.SetPersistentMeta(Plugin.MuteKey, state.ToString(), client.ClientId);
        client.SetAdditionalProperty(Plugin.MuteKey, state);
    }
}
