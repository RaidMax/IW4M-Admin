namespace SharedLibraryCore.Events.Game.GameScript.Zombie;

// T6 Tranzit / Die Rise / Buried bank transaction. Engine fee structure: $1000
// per deposit increment (no fee), $1000 gross per withdrawal with a $100 fee on
// top (level.ta_vaultfee) — the fee is NOT surfaced here; Amount carries the
// principal only.
public class BankTransactionGameEvent : ClientGameEvent
{
    public bool IsDeposit { get; init; }
    public int Amount { get; init; }
}
