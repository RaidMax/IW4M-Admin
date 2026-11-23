using SharedLibraryCore.Interfaces;

namespace SharedLibraryCore.Helpers;

/// <summary>
/// Inert placeholder substituted for a plugin that could not be constructed
/// because it targets API this (older) IW4MAdmin host does not have. It keeps the
/// plugin out of the way without putting a null into the resolved
/// <see cref="IPluginV2"/> / <see cref="IPlugin"/> collections (which the DI
/// container cannot drop mid-enumeration). It does nothing and subscribes to
/// nothing. The user has already been told to update via
/// <see cref="PluginApiCompatibility.NotifyNewerApiRequired"/>.
/// </summary>
public sealed class UnavailablePlugin(string name) : IPluginV2, IPlugin
{
    public string Name { get; } = name;
    public string Author => "unavailable";

    // IModularAssembly/IPluginV2 version is a string; legacy IPlugin version is a
    // float, so the latter is implemented explicitly to avoid the clash.
    public string Version => "0";
    float IPlugin.Version => 0f;

    public Task OnLoadAsync(IManager manager) => Task.CompletedTask;
    public Task OnUnloadAsync() => Task.CompletedTask;
    public Task OnEventAsync(GameEvent gameEvent, Server server) => Task.CompletedTask;
    public Task OnTickAsync(Server server) => Task.CompletedTask;
}
