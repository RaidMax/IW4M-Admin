using SharedLibraryCore.Dtos;

namespace WebCommon.Services;

/// <summary>
/// A typed, host-declared extension point. Pairs the slot's wire identity (<see cref="Name"/>,
/// matched against <see cref="IRenderSlotProvider.SlotName"/>) with the parameter contract
/// (<typeparamref name="TParams"/>) that a filling widget receives. Reference the strongly-typed
/// members of <see cref="RenderSlots"/> instead of raw slot-name strings so the full host↔plugin
/// flow is navigable by Find Usages / Go To Definition and checked by the compiler.
/// </summary>
/// <typeparam name="TParams">
/// The record a filling widget receives as its single <c>[Parameter] public TParams Model { get; set; }</c>
/// (the parameter must be named <c>Model</c> — see <c>PluginRenderSlot</c>). Keep this contract additive
/// (init-only properties): plugins compile against it, so renaming/removing a member or switching to a
/// positional record breaks pre-built plugin bundles. Adding members is safe (the host constructs the
/// record; an older widget simply ignores new members).
/// </typeparam>
public sealed record RenderSlot<TParams>(string Name) where TParams : class;

/// <summary>
/// The catalog of render slots the host exposes in its pages. Each entry is the single source of truth
/// for one slot: its wire name and its parameter contract. A plugin fills a slot by registering an
/// <see cref="IRenderSlotProvider"/> whose <see cref="IRenderSlotProvider.SlotName"/> equals
/// <c>RenderSlots.X.Name</c>, pointing at a widget that declares <c>[Parameter] public TParams Model</c>.
/// </summary>
public static class RenderSlots
{
    /// <summary>
    /// Player Advanced Stats page, below the stat-card grid. Intended for per-player rich stat
    /// extensions (e.g. zombie match history).
    /// </summary>
    public static readonly RenderSlot<ClientAdvancedStatsParams> ClientAdvancedStats = new("client-advanced-stats");

    /// <summary>
    /// Server card — a live per-server indicator rendered in both the desktop header row and the
    /// mobile detail panel (distinguished by <see cref="ServerCardParams.Variant"/>).
    /// </summary>
    public static readonly RenderSlot<ServerCardParams> ServerCardZombie = new("server-card-zombie");
}

/// <summary>Parameter contract for the <see cref="RenderSlots.ClientAdvancedStats"/> slot.</summary>
public sealed record ClientAdvancedStatsParams
{
    /// <summary>The profile owner's client id.</summary>
    public required int ClientId { get; init; }

    /// <summary>The stats server scope (server endpoint), or null for the aggregate/all-servers view.</summary>
    public string? ServerId { get; init; }
}

/// <summary>Placement of a <see cref="RenderSlots.ServerCardZombie"/> widget within the server card.</summary>
public enum ServerCardVariant
{
    /// <summary>Desktop header row (compact, typically interactive).</summary>
    Desktop,

    /// <summary>Mobile detail panel row (static).</summary>
    Mobile
}

/// <summary>Parameter contract for the <see cref="RenderSlots.ServerCardZombie"/> slot.</summary>
public sealed record ServerCardParams
{
    /// <summary>The server the card represents.</summary>
    public required ServerInfo Server { get; init; }

    /// <summary>Where in the card this instance renders.</summary>
    public ServerCardVariant Variant { get; init; } = ServerCardVariant.Desktop;
}
