#nullable enable
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Data.Models.Client;

namespace Data.Models.Zombie;

public enum EventLogType
{
    Default = 0,
    PerformanceCluster = 1,
    DamageTaken = 2,
    Downed = 3,
    Died = 4,
    Revived = 5,
    WasRevived = 6,
    PerkConsumed = 7,
    PowerupGrabbed = 8,
    RoundCompleted = 9,
    JoinedMatch = 10,
    LeftMatch = 11,
    MatchStarted = 12,
    MatchEnded = 13,
    WeaponPurchased = 14,
    WeaponUpgraded = 15,
    BoxTake = 16,
    BoxPass = 17,
    BoxTeddy = 18,
    DoorPurchased = 19,
    TrapActivated = 20,
    BuildComplete = 21,
    WeaponAbandoned = 22,
    EasterEggCompleted = 23,
    /// <summary>
    /// Per-step EE progress marker. <see cref="ZombieEventLog.TextualValue"/> holds
    /// the step key (e.g. "t4_vr_radio_1"); <see cref="ZombieEventLog.NumericalValue"/>
    /// holds the round number at fire time. Match-level event (no SourceClientId).
    /// Idempotent — premium handler dedups via (MatchId, EventType, TextualValue).
    /// </summary>
    EasterEggStep = 24,
    /// <summary>
    /// Map power activated. <see cref="ZombieEventLog.SourceClientId"/> populated
    /// when a player flipped the switch (use trigger), null when world-triggered
    /// (scripted auto-activation, devgui). <see cref="ZombieEventLog.NumericalValue"/>
    /// holds the round number at fire time.
    /// </summary>
    PowerOn = 25,
    /// <summary>
    /// Map power lost — currently only fired by TranZit (bus power loss / pylon).
    /// <see cref="ZombieEventLog.SourceClientId"/> typically null since TranZit
    /// power-off is a world event. <see cref="ZombieEventLog.NumericalValue"/>
    /// holds the round number at fire time.
    /// </summary>
    PowerOff = 26,
    /// <summary>
    /// Bank deposit — T6 Tranzit / Die Rise / Buried only.
    /// <see cref="ZombieEventLog.NumericalValue"/> holds the amount (always 1000;
    /// engine charges $1000 per increment, $100 fee on withdrawal not deposit).
    /// </summary>
    BankDeposit = 27,
    /// <summary>
    /// Bank withdrawal — T6 Tranzit / Die Rise / Buried only.
    /// <see cref="ZombieEventLog.NumericalValue"/> holds the gross principal
    /// (1000); the $100 fee deducted on top is not surfaced.
    /// </summary>
    BankWithdraw = 28,
    /// <summary>
    /// Weapon Locker store — T6 Tranzit / Die Rise / Buried only.
    /// <see cref="ZombieEventLog.TextualValue"/> holds the engine weapon name.
    /// </summary>
    WeaponLockerStore = 29,
    /// <summary>
    /// Weapon Locker retrieve — T6 Tranzit / Die Rise / Buried only.
    /// <see cref="ZombieEventLog.TextualValue"/> holds the engine weapon name.
    /// </summary>
    WeaponLockerRetrieve = 30,
    /// <summary>
    /// Gobble Gum activated — player consumed an "activated"-type gum (BO3/T7 only).
    /// <see cref="ZombieEventLog.TextualValue"/> holds the BGB engine name
    /// (e.g. <c>zm_bgb_perkaholic</c>).
    /// </summary>
    GobbleGumActivated = 31,
    /// <summary>
    /// Gobble Gum taken from machine — player paid the machine cost and grabbed
    /// the gum (BO3/T7 only). <see cref="ZombieEventLog.TextualValue"/> holds
    /// the BGB engine name; <see cref="ZombieEventLog.NumericalValue"/> holds
    /// the machine cost (often 0 for free, 1500 for paid).
    /// </summary>
    GobbleGumTaken = 32,
    /// <summary>
    /// Gobble Gum abandoned — player paid the machine but didn't grab; cost
    /// forfeited (ghost-ball cases excluded; BO3/T7 only).
    /// <see cref="ZombieEventLog.TextualValue"/> holds the BGB engine name;
    /// <see cref="ZombieEventLog.NumericalValue"/> holds the cost lost.
    /// </summary>
    GobbleGumAbandoned = 33
}

public class ZombieEventLog : DatedRecord
{
    [Key]
    public long ZombieEventLogId { get; set; }

    [NotMapped] public override long Id => ZombieEventLogId;
    
    public EventLogType EventType { get; set; }

    // Navs mirror their nullable FKs — match-level events (e.g. EasterEggStep) have
    // no SourceClientId, world-triggered events no client at all. The previous
    // non-nullable declarations promised navs that are legitimately null at runtime.
    public int? SourceClientId { get; set; }
    [ForeignKey(nameof(SourceClientId))]
    public virtual EFClient? SourceClient { get; set; }

    public int? AssociatedClientId { get; set; }
    [ForeignKey(nameof(AssociatedClientId))]
    public virtual EFClient? AssociatedClient { get; set; }

    public double? NumericalValue { get; set; }
    public string? TextualValue { get; set; }

    public int? MatchId { get; set; }
    [ForeignKey(nameof(MatchId))]
    public virtual ZombieMatch? Match { get; set; }
}
