# ZombieStats GSC

Per-engine GSC instrumentation that emits zombie-mode telemetry to IW4MAdmin via the server game log. Feeds the `ZombieStats` plugin (free) + `ZombieStatsPremium` plugin (private) for stat tracking, leaderboards, match history, Easter Egg detection, pace metrics, and timeline rendering.

## Files

| File | Game | Runtime | Compile |
|------|------|---------|---------|
| `_zm_stats_t4.gsc` | World at War (T4) | Plutonium T4 | interpreted |
| `_zm_stats_t5.gsc` | Black Ops 1 (T5) | Plutonium T5 | interpreted |
| `_zm_stats_t6.gsc` | Black Ops 2 (T6) | Plutonium T6 | interpreted |
| `_zm_stats_t7.gsc` | Black Ops 3 (T7) | T7x AlterWare | source |
| `_zm_stats_t7.compiled.gsc` | Black Ops 3 (T7) | T7x AlterWare | **compiled bytecode** (~37 KB; deploy this) |
| `FEATURE_MATRIX.md` | — | — | cross-engine emission inventory |

## Wire format

All four files emit a consolidated `GSE;...` log format on stdout:

| Code | Meaning |
|------|---------|
| `K`  | Player kill / death |
| `D`  | Player damage |
| `AD` | Actor (zombie) damage |
| `AK` | Actor killed |
| `RD` | Per-player round data |
| `RC` | Round complete |
| `ZP` | Per-player zone/economy event (perks, powerups, weapons, box, doors, traps, builds, revives, gum, bank, locker) |
| `ZW` | World/round event (power, specials, zombies-remaining, easter-egg) |

See [`FEATURE_MATRIX.md`](FEATURE_MATRIX.md) for the full subtype inventory, per-engine support, and known gaps.

## Installation

### T4 / T5 / T6 (Plutonium — interpreted)

Drop the `.gsc` into the appropriate Plutonium storage scripts directory and reload the map. Plutonium parses the file at level start.

| Game | Path |
|------|------|
| T4 | `%LOCALAPPDATA%\Plutonium\storage\t4\scripts\sp\` |
| T5 | `%LOCALAPPDATA%\Plutonium\storage\t5\scripts\sp\zom\` |
| T6 | `%LOCALAPPDATA%\Plutonium\storage\t6\scripts\zm\` |

### T7 (T7x AlterWare — compiled bytecode)

T7 is the only engine that requires compilation. The runtime loads compiled GSC bytecode from:

```
<bo3_install>\t7x\custom_scripts\
```

Deploy `_zm_stats_t7.compiled.gsc` from this directory into `<bo3_install>\t7x\custom_scripts\`. The file is identified internally by magic bytes `80 47 53 43 0D 0A` (`ÇGSC\r\n`).

**Suffix gate** — T7x enforces `filename.endsWith(".gsc")` on every file in `custom_scripts/`. Cerberus's native `.gscc` output is rejected as `failed to load due to invalid suffix`. The `.compiled.gsc` double-extension passes the gate (last 4 chars = `.gsc`) and disambiguates from the `_zm_stats_t7.gsc` source filename.

**Benign DB error** — T7x logs `[DB] Error: Could not find scriptparsetree "custom_scripts/<filename>"` on every custom_scripts/ load, regardless of compile settings. Confirmed harmless: the script loads and executes correctly, events flow normally to the game log. Believed to come from T7x's secondary DB asset registry lookup running after the primary runtime load succeeded. No known suppression. Verified 2026-05-14 against running T7x server with full event emission in `games_zm.log`.

## T7 build chain

Required when `_zm_stats_t7.gsc` changes. PowerShell only — Bash/MSYS can't load the required Windows DLLs.

```powershell
$root = "C:\Users\Amos\_OtherProjects\CoD Scripts\T7\_COMPILATION_TOOLS\ModTools\T7 Mod Tools Stripped"
$name = "_zm_stats_t7"
$env:TA_GAME_PATH         = "$root\"
$env:TA_LOCAL_ASSET_CACHE = "$root\share\assetconvert\"
$env:TA_TOOLS_PATH        = "$root\"
$proj = "$root\usermaps\$name"
Remove-Item -Recurse -Force "$proj" -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$proj\scripts","$proj\zone_source\loc" | Out-Null

# Source must keep its .gsc extension (NOT .compiled.gsc) — the linker uses
# everything after the first dot as the extension for #using resolution.
Copy-Item "<repo>\GameFiles\ZombieStats\_zm_stats_t7.gsc" "$proj\scripts\$name.gsc" -Force
Set-Content "$proj\zone_source\$name.zone"     "scriptparsetree,scripts/$name.gsc"
Set-Content "$proj\zone_source\loc\$name.zone" ""
& "$root\bin\linker_modtools.exe" -language english -modsource $name
"" | & "C:\Users\Amos\_OtherProjects\CoD Scripts\T7\_COMPILATION_TOOLS\Cerberus\Cerberus.CLI.exe" "$proj\zone\$name.ff"

# Cerberus extracts to: ...\ExtractedScripts\Black Ops III\scripts\$name.gscc
# Rename .gscc -> .compiled.gsc and copy back to the repo:
Copy-Item ".\Cerberus\ExtractedScripts\Black Ops III\scripts\$name.gscc" `
          "<repo>\GameFiles\ZombieStats\$name.compiled.gsc" -Force
```

**Gotchas:**

1. Stripped tools need MSVC++ 2012 x64 Redist (`MSVCP110.dll` / `MSVCR110.dll`) — install `vcredist_x64.exe`.
2. Project must live under `usermaps\<name>\`, not `mods\<name>\` (linker anchors `-modsource` to `usermaps\`).
3. The zone filename must match the `-modsource` argument.
4. The localized pass needs an empty `zone_source\loc\<name>.zone` file.
5. Cerberus blocks on a "press Enter to exit" prompt — pipe empty string to release.
6. Always invoke `linker_modtools.exe` via PowerShell. The Bash/MSYS exec wrapper doesn't honour the Windows DLL search path.
7. **Source filename must end in `.gsc` only** — not `.compiled.gsc`. The linker treats everything after the first `.` as the extension for `#using` directive resolution; a `<name>.compiled.gsc` source breaks every `#using scripts\shared\X;` because it tries to resolve `scripts\shared\X.compiled.gsc`. Apply the `.compiled.gsc` rename to the Cerberus output, not the source.

## Architecture

### Detection patterns

| Mechanic | Pattern | Engines |
|----------|---------|---------|
| Perk buy | T4: weapon-switch poll (no engine notify exists); T5/T6/T7: `perk_bought` notify | all |
| Powerup grab | Proximity poll on `script_model` entities — do NOT hook `level.zombie_powerup_grab_func` (replacing it disables the effect) | all |
| Pack-a-Punch | Lock-first attribution (`WatchPapTriggerForBuyer` + `WatchPapTakenFlag` + `WatchPapTimeoutFlag` + `WatchPapDisconnectFlag` + `WatchPapOutcome`) | all |
| Mystery box | Notify-driven with 3-tier user resolution + scoped teddy-suppression | all |
| Bank | Poll `self.account_value` deltas (deposits silent; withdrawals emit a notify but we poll for both) | T6 Tranzit/DieRise/Buried |
| Weapon locker | Poll `self.stored_weapon_data` transitions (avoid namespace call to prevent load errors on no-locker maps) | T6 Tranzit/DieRise/Buried |
| Easter Eggs | T4/T5/T6: canonical terminal-notify wait → `easter_egg;complete`. T7: per-step emission → server-side derives completion when all configured steps log. Both paths set `EasterEggOccurredAt` | all |
| Special rounds | Per-round `level.flag["<type>_round"]` poll → `ZW;round_special;<round>;<type>` | T4: dog. T5: dog/monkey/thief. T6: dog/leaper. T7: dog/monkey/wasp/spider/robot/quad/boss/ee |
| Gobble Gums | `bgb_activation` player notify + `user_grabbed_bgb` machine notify + `bgb_machine_accessed` (refund-filtered for ghost balls) | T7 only |

### Match ID stitching

All four engines seed `sv_iw4m_zm_matchid` at init with `randomint(1000000) + "_" + randomint(1000000)` (~10¹² collision space). `gettime()` is deliberately avoided because the engine clock returns 0 at init on T4/T5/T6. The lookup index is `(ServerId, GameMatchId)` so cross-server collisions are harmless.

### Concurrent K/RD drain

All four wait 0.1s between K (death) emission and RD/RC (round data / complete) to prevent the C# side's concurrent processing from rolling up RD before the K death increment lands.

## Caveats

- **Special rounds break Seconds Per Horde** unless the SPH calculator skips them via the `ZW;round_special` signal (handled server-side).
- **T7 hash literals are frozen** because BO3 is end-of-life — `#"hash_21edb6b6"` (SoE Arnie) and `#"hash_6460283a"` (GK terminal) will not change but cannot be reverse-resolved to names (verified absent from the 3287-name candidate dict via `hash-name.ps1`).
- **T4 perk-buy** edge case: if a player buys a perk and is downed within 0.1s, the emission is dropped (no `perk_bought` notify exists on W@W; weapon-switch poll is the only path).
- **Self-revive** is emitted as `revive;self` on T5/T6/T7 only. T4 has no self-revive mechanic in W@W.

## Related

- [`FEATURE_MATRIX.md`](FEATURE_MATRIX.md) — cross-engine feature support matrix
- [`../README.md`](../README.md) — GameFiles overview (Game Interface, AntiCheat, ZombieStats)
- `Plugins/ZombieStats/` — free plugin (event parsing, IZombieStatsEnhancer interface)
- `_PRIVATE/ZombieStatsPremium/` — premium plugin (match history, leaderboards, EE detection, skill scoring)
