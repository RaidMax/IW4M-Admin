#nullable enable

using Data.Models.Zombie;

namespace SharedLibraryCore.Interfaces;

/// <summary>
/// Provides per-player zombie match history with round breakdowns and event timelines.
/// Implemented by the premium plugin.
/// </summary>
public interface IZombieMatchHistoryService
{
    Task<List<ZombieMatchHistoryMatch>> GetPlayerMatchHistoryAsync(int clientId, string? serverEndpoint,
        int offset = 0, int count = 5);

    /// <summary>
    /// Returns full match detail with all players' round breakdowns and event timelines.
    /// Used by the leaderboard's expandable match detail view.
    /// </summary>
    Task<ZombieMatchDetail?> GetMatchDetailAsync(int matchId);
}

public class ZombieMatchDetail
{
    public int MatchId { get; set; }
    public string Map { get; set; } = string.Empty;
    public DateTimeOffset Date { get; set; }
    public string? ServerName { get; set; }
    public double DurationMinutes { get; set; }
    public int HighestRound { get; set; }
    public bool Completed { get; set; }
    public List<ZombieMatchDetailPlayer> Players { get; set; } = [];

    /// <summary>
    /// Distinct iconic buildables completed in this match (capped at
    /// <see cref="BuildablesTotal"/>). Sourced from event-log entries of type
    /// <c>BuildComplete</c>, classified against <c>MapBuildableConfig</c>'s iconic
    /// list. For unconfigured maps (custom maps, T4/T5), this falls back to all
    /// distinct names built — same value <c>BuildableNames.Count</c>.
    /// </summary>
    public int BuildablesBuilt { get; set; }

    /// <summary>
    /// Total iconic buildables on this map (per <c>MapBuildableConfig</c>), or null
    /// when the map has no configured total — custom maps, T4/T5 (no buildable
    /// system), or unmapped maps. Null hides the denominator in the UI.
    /// </summary>
    public int? BuildablesTotal { get; set; }

    /// <summary>
    /// Iconic buildables completed (distinct, in build order) — paired raw key +
    /// resolved display name so the UI never has to call back into a config the
    /// premium plugin owns. Empty on configured-but-nothing-built maps.
    /// </summary>
    public List<BuildableEntry> BuildableNames { get; set; } = [];

    /// <summary>
    /// Full iconic buildable inventory for the map — every iconic item the
    /// engine could register on this map, in canonical order, regardless of
    /// whether it was built. Lets the UI render a "checklist" view (muted
    /// chip when not built, filled when built — mirrors the EE step grid).
    /// Empty for unconfigured maps (custom maps, T4/T5 — no iconic concept);
    /// the UI falls back to listing only <see cref="BuildableNames"/> chips
    /// in that case so unconfigured maps still show what was built.
    /// </summary>
    public List<BuildableEntry> BuildableInventory { get; set; } = [];

    /// <summary>
    /// Distinct non-iconic buildables completed — side-quest items, PaP-on-Tranzit,
    /// situational extras, etc. Always 0 on unconfigured maps (everything lands in
    /// iconic by default). Drives the "+N extra" chip alongside the All-Built badge.
    /// </summary>
    public int ExtraBuildablesBuilt { get; set; }

    /// <summary>
    /// Non-iconic buildables completed (distinct, in build order) — paired raw
    /// key + resolved display name, same shape as <see cref="BuildableNames"/>.
    /// Empty on unconfigured maps.
    /// </summary>
    public List<BuildableEntry> ExtraBuildableNames { get; set; } = [];

    /// <summary>Round at which the EE fired (when known).</summary>
    public int? EasterEggRound { get; set; }

    /// <summary>
    /// UTC timestamp at which the EE fired. Authoritative "EE happened" signal —
    /// non-null implies completed. Drives the scrubber timeline marker.
    /// </summary>
    public DateTimeOffset? EasterEggOccurredAt { get; set; }

    /// <summary>
    /// Per-quest EE progress for this map. A map may have multiple distinct quests
    /// (Der Riese ships with both the Meteor song-egg and the Fly Trap teleporter
    /// quest), each tracked independently. Empty when the map has no configured
    /// quests. UI renders one chip / progress card per entry.
    /// </summary>
    public List<EasterEggQuestProgress> EasterEggQuests { get; set; } = [];

    /// <summary>
    /// Chronological power-state transitions observed during the match (chronological
    /// = oldest first). Stock single-switch maps yield a single On entry; TranZit can
    /// produce multi-step On→Off→On chains via bus power loss / pylon repair.
    /// Empty for maps without a power switch (Nacht der Untoten) — UI hides the row.
    /// </summary>
    public List<PowerStateChange> PowerStateChanges { get; set; } = [];
}

/// <summary>
/// One power-state transition observed during the match. <see cref="PlayerName"/>
/// is null when the change wasn't player-attributed (TranZit world events,
/// scripted activation). UI shows world-attributed transitions without a name.
/// </summary>
public sealed class PowerStateChange
{
    /// <summary>true = ON, false = OFF.</summary>
    public bool IsOn { get; set; }
    /// <summary>Round at which the change fired. Null when fired before round 1.</summary>
    public int? Round { get; set; }
    /// <summary>UTC timestamp of the change (drives chronological ordering + scrubber markers).</summary>
    public DateTimeOffset OccurredAt { get; set; }
    /// <summary>Activating player display name; null for world-attributed events.</summary>
    public string? PlayerName { get; set; }
    /// <summary>Activating player ClientId; null for world-attributed events. Drives profile-link rendering.</summary>
    public int? PlayerClientId { get; set; }
}

/// <summary>
/// Per-quest progress: configured inventory + observed step records + completion
/// state. One <see cref="EasterEggQuestProgress"/> per quest on the map; a quest
/// is "complete" iff every step in <see cref="Inventory"/> has a matching record
/// in <see cref="Steps"/>, OR the quest is canonical-notify driven and the
/// canonical event fired.
/// </summary>
public sealed class EasterEggQuestProgress
{
    /// <summary>Quest id ("song", "flytrap"). Stable, matches MapEasterEggConfig.</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>Translation key for the quest's full display title (progress card header).</summary>
    public string LocKey { get; set; } = string.Empty;

    /// <summary>
    /// Translation key for the quest's compact label (titlebar chip / mini-strip).
    /// Always quest-specific so two chips on the same map don't both read "EE R{n}".
    /// </summary>
    public string ShortLocKey { get; set; } = string.Empty;

    /// <summary>Phosphor icon name for the quest's titlebar chip.</summary>
    public string Icon { get; set; } = string.Empty;

    /// <summary>
    /// True when the map has a terminal GSC notify for this quest (T6 main quests,
    /// Der Riese fly trap). False when completion is derived from "all steps logged"
    /// (T4 song eggs).
    /// </summary>
    public bool HasCanonicalNotify { get; set; }

    /// <summary>Configured step inventory in static order (drives checklist render).</summary>
    public List<EasterEggStepInventoryEntry> Inventory { get; set; } = [];

    /// <summary>Logged step records for this quest, in fire order.</summary>
    public List<EasterEggStepRecord> Steps { get; set; } = [];

    /// <summary>Total steps configured on the quest. Equal to <see cref="Inventory"/>.Count.</summary>
    public int Total { get; set; }

    /// <summary>Number of distinct steps logged so far. Equal to <see cref="Steps"/>.Count.</summary>
    public int Completed { get; set; }

    /// <summary>True when the quest is fully complete (all steps logged OR canonical fired).</summary>
    public bool IsComplete { get; set; }

    /// <summary>Round of the most recent step (or canonical fire). Drives partial-chip "R{n}" suffix.</summary>
    public int? LastRound { get; set; }

    /// <summary>Round at which the quest was completed. Null until complete.</summary>
    public int? CompletedRound { get; set; }

    /// <summary>UTC timestamp when the quest completed. Null until complete.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// For branching quests (Maxis-vs-Richtofen on TranZit etc.): the group id
    /// shared by both variants. Null on single-variant quests. <see cref="Id"/>,
    /// <see cref="LocKey"/>, etc. on a branching entry refer to the GROUP — the
    /// active variant's identity lives in <see cref="ActiveVariantId"/> /
    /// <see cref="ActiveVariantLocKey"/> below. Lifetime EE counts dedupe by this
    /// group id, so completing Maxis in one match and Richtofen in another counts
    /// as 2 EE completions of the same achievement, not 2 separate achievements.
    /// </summary>
    public string? BranchGroupId { get; set; }

    /// <summary>
    /// On branching quests: the variant locked in this match (first variant to
    /// fire any step). Null when the group has had no steps logged yet (no
    /// variant chosen) and on non-branching quests. Steps/Inventory/Total/etc.
    /// reflect this variant when set; show 0/null counts when unset.
    /// </summary>
    public string? ActiveVariantId { get; set; }

    /// <summary>Translation key for the active variant's full title (e.g. "Maxis path").</summary>
    public string? ActiveVariantLocKey { get; set; }

    /// <summary>Translation key for the active variant's compact label (chip subtitle).</summary>
    public string? ActiveVariantShortLocKey { get; set; }
}

public sealed class EasterEggStepInventoryEntry
{
    /// <summary>Step key — matches <see cref="EasterEggStepRecord.Key"/> when fired.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Translation key for the step's display label.</summary>
    public string LocKey { get; set; } = string.Empty;

    /// <summary>Phosphor icon name (e.g. "ph-radio") for the step's visual marker.</summary>
    public string Icon { get; set; } = string.Empty;
}

public sealed class EasterEggStepRecord
{
    /// <summary>Step key (e.g. "t4_vr_radio_1"). Matches MapEasterEggConfig entries.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Round number at which the step fired. Null when fired pre-round-1.</summary>
    public int? RoundNumber { get; set; }

    /// <summary>UTC timestamp the step was logged.</summary>
    public DateTimeOffset OccurredAt { get; set; }
}

public class ZombieMatchDetailPlayer
{
    public int ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Kills { get; set; }
    public int Deaths { get; set; }
    public int Downs { get; set; }
    public int Revives { get; set; }
    public long PointsEarned { get; set; }

    /// <summary>
    /// Total points spent by this player across the match (Pack-a-Punch, doors,
    /// box draws, perks, traps, etc). Surfaces in the per-player stat card as
    /// the subtitle to Net Points so the user can see earned-vs-spent context.
    /// </summary>
    public long PointsSpent { get; set; }

    public int Headshots { get; set; }

    /// <summary>
    /// Subset of <see cref="Kills"/> that were headshots. Used to compute the
    /// "HS%" stat (HeadshotKills / Kills * 100) — same metric the leaderboard
    /// scoreboard surfaces. <see cref="Headshots"/> is total headshot HITS
    /// (which can exceed kills when a body absorbs multiple HS hits before
    /// dying); HeadshotKills is the kill-attribution subset.
    /// </summary>
    public int HeadshotKills { get; set; }

    public long DamageDealt { get; set; }
    public int DamageReceived { get; set; }

    /// <summary>
    /// Rounds in this match where this player had at least one other tracked teammate.
    /// Null on legacy matches.
    /// </summary>
    public int? AssistedRounds { get; set; }

    /// <summary>
    /// First round at which the player became "solo to the end". Drives the
    /// "Solo from R&lt;N&gt;" badge. Null if the player was assisted to the final round.
    /// </summary>
    public int? SoloFromRound { get; set; }

    /// <summary>
    /// Whether this player meets the canonical match-qualifier rule (same one driving
    /// the leaderboard listing). Drives the scrubber's default lane visibility — the
    /// timeline shows qualified players first, with a toggle to surface drop-ins.
    /// </summary>
    public bool IsQualified { get; set; }

    public List<ZombieMatchHistoryRound> Rounds { get; set; } = [];
    public List<ZombieMatchHistoryEvent> Events { get; set; } = [];
}

public class ZombieMatchHistoryMatch
{
    public int MatchId { get; set; }
    public string Map { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string? ServerName { get; set; }
    public int HighestRound { get; set; }
    public double DurationMinutes { get; set; }
    public long Kills { get; set; }
    public long Deaths { get; set; }
    public long PointsEarned { get; set; }
    public bool Completed { get; set; }

    /// <summary>Round at which the EE fired (when known).</summary>
    public int? EasterEggRound { get; set; }

    /// <summary>
    /// UTC timestamp at which the EE fired. Non-null implies completed.
    /// </summary>
    public DateTimeOffset? EasterEggOccurredAt { get; set; }

    /// <summary>Per-quest EE progress for this match's map. Empty when unconfigured.</summary>
    public List<EasterEggQuestProgress> EasterEggQuests { get; set; } = [];

    /// <summary>Iconic buildables completed (capped at <see cref="BuildablesTotal"/>; falls back to all distinct on unconfigured maps).</summary>
    public int BuildablesBuilt { get; set; }

    /// <summary>Total iconic buildables on this map (per <c>MapBuildableConfig</c>), or null.</summary>
    public int? BuildablesTotal { get; set; }

    /// <summary>Distinct non-iconic buildables completed (side-quest / extras). 0 on unconfigured maps.</summary>
    public int ExtraBuildablesBuilt { get; set; }

    /// <summary>This player's total downs across the match — drives the "Personal No-Down" badge.</summary>
    public int Downs { get; set; }

    /// <summary>
    /// Rounds in this match where this player had at least one other tracked teammate.
    /// Null on legacy matches that pre-date the metric.
    /// </summary>
    public int? AssistedRounds { get; set; }

    /// <summary>
    /// First round at which the player became "solo to the end". Drives the
    /// "Solo from R&lt;N&gt;" badge. Null if the player was assisted to the final round
    /// or the metric wasn't computed for this match (legacy data).
    /// </summary>
    public int? SoloFromRound { get; set; }

    public List<ZombieMatchHistoryRound> Rounds { get; set; } = [];
    public List<ZombieMatchHistoryEvent> Events { get; set; } = [];

    /// <summary>
    /// Power-state transitions during this match. Same shape and semantics as
    /// <see cref="ZombieMatchDetail.PowerStateChanges"/>. Empty for maps without power.
    /// Surfaces on the per-client scrubber as match-level marker(s).
    /// </summary>
    public List<PowerStateChange> PowerStateChanges { get; set; } = [];
}

public class ZombieMatchHistoryRound
{
    public int RoundNumber { get; set; }
    public long Kills { get; set; }
    public long Deaths { get; set; }
    public long Downs { get; set; }
    public long Revives { get; set; }
    public int Points { get; set; }
    public double DurationSeconds { get; set; }

    /// <summary>
    /// Player count snapshot at the moment this round began — frozen so the
    /// EMA cell key matches the round's fixed zombie spawn count regardless of
    /// mid-round joins/leaves. Null on legacy rounds pre-dating the column
    /// (readers can fall back to match-level <c>PlayerCount</c> as a best-guess).
    /// </summary>
    public int? PlayerCountAtRoundStart { get; set; }

    /// <summary>
    /// Pace classification of this round's duration vs the population EMA for
    /// (map, round, player count). Null when no EMA sample exists yet for the
    /// bucket — UI renders neutral. Computed server-side; the UI just maps
    /// the band to a colour.
    /// </summary>
    public PaceBand? PaceBand { get; set; }

    /// <summary>
    /// Signed delta vs typical, expressed as a fraction (0.12 = 12% slower than
    /// typical, -0.08 = 8% faster). Null when <see cref="PaceBand"/> is null.
    /// Surfaces in the tooltip alongside the band colour.
    /// </summary>
    public double? PaceRatio { get; set; }

    /// <summary>
    /// EMA value (in seconds) the round was compared against — the "typical"
    /// duration shown in the tooltip. Null when <see cref="PaceBand"/> is null.
    /// </summary>
    public double? PaceTypicalSeconds { get; set; }

    /// <summary>
    /// Special-round classification when the round replaced the regular zombie
    /// spawn pool (Dog / Monkey / Leaper). Null on normal rounds. Sourced from
    /// GSC <c>GSE;RS</c> emissions captured at round start. Drives the Round
    /// Breakdown UI badge.
    /// </summary>
    public ZombieSpecialRoundType? SpecialType { get; set; }
}

/// <summary>
/// Round-pace classification bands. Ordered fastest → slowest. Maps to a
/// 5-colour scale on the webfront (deep green → grey → deep red).
/// </summary>
public enum PaceBand
{
    /// <summary>&gt; 15% faster than the population EMA.</summary>
    MuchFaster,

    /// <summary>5–15% faster than the population EMA.</summary>
    Faster,

    /// <summary>Within ±5% of the population EMA — typical pace.</summary>
    Neutral,

    /// <summary>5–15% slower than the population EMA.</summary>
    Slower,

    /// <summary>&gt; 15% slower than the population EMA.</summary>
    MuchSlower,
}

/// <summary>
/// A pre-processed, display-ready event for the match timeline.
/// All mapping from internal event types to labels/categories is done by the provider.
/// </summary>
public class ZombieMatchHistoryEvent
{
    /// <summary>Display time string (e.g. "20:03:05").</summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>Time as total seconds from midnight, for timeline positioning.</summary>
    public double Seconds { get; set; }

    /// <summary>Human-readable label (e.g. "Downed", "Double Points", "Round 5").</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>
    /// Visual category for rendering. One of: "round", "powerup", "danger", "critical", "success",
    /// "perk", "weapon", "box", "box-pass", "door", "trap", "build", "session-join", "session-leave".
    /// The component maps these to colors/icons.
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// For round-category events, the round number this marker represents. Lets the
    /// timeline component compute gap ranges (where the player skipped rounds) without
    /// parsing the <see cref="Label"/> string. Null for non-round events.
    /// </summary>
    public int? RoundNumber { get; set; }

    /// <summary>
    /// Pace band for the round this event represents (round-category events only).
    /// Lets the timeline tint the round marker AND surface a "vs avg" annotation
    /// so live viewers see whether each completed round was on/off pace at a glance.
    /// Null on non-round events or when no EMA sample exists yet.
    /// </summary>
    public PaceBand? PaceBand { get; set; }

    /// <summary>Signed delta vs typical (0.12 = 12% slower). Null when <see cref="PaceBand"/> is null.</summary>
    public double? PaceRatio { get; set; }

    /// <summary>EMA value (in seconds) the round was compared against. Null when <see cref="PaceBand"/> is null.</summary>
    public double? PaceTypicalSeconds { get; set; }
}

/// <summary>
/// One buildable/craftable, paired with its community-recognisable display name.
/// <see cref="Key"/> is the GSC-internal name as registered via
/// <c>add_zombie_buildable</c> / <c>add_zombie_craftable</c> and stored in
/// <c>BuildComplete.TextualValue</c>; <see cref="DisplayName"/> is what the
/// webfront renders. Pairing them in the DTO means the UI never has to know
/// about premium-side display config.
/// </summary>
public sealed class BuildableEntry
{
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
