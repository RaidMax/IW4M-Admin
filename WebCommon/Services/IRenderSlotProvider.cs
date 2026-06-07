namespace WebCommon.Services;

/// <summary>
/// Registered by a plugin (via <c>RegisterDependencies</c>) to contribute a Blazor component
/// into a host-declared render slot — see <c>PluginRenderSlot</c>. The host depends only on
/// this abstraction and never on the plugin: it resolves <see cref="IEnumerable{IRenderSlotProvider}"/>
/// and renders the matching providers with <c>DynamicComponent</c>. With no plugin registered,
/// the slot renders nothing.
/// </summary>
public interface IRenderSlotProvider
{
    /// <summary>The slot this provider fills, matched against <c>PluginRenderSlot.SlotName</c>.</summary>
    string SlotName { get; }

    /// <summary>Component type to render. Activated by <c>DynamicComponent</c>.</summary>
    Type ComponentType { get; }

    /// <summary>Render order when multiple providers fill one slot (ascending).</summary>
    int Order { get; }
}
