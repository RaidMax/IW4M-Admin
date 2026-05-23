# Zombie Stats GSC — Feature Support Matrix

Cross-engine inventory of what each `_zm_stats_t<n>.gsc` instrumentation script
emits to IW4MAdmin. All four are on the consolidated `GSE;ZP;` (per-player) /
`GSE;ZW;` (world) wire dialect — no pre-consolidation `RS`/`ZR`/`PWR`/`EE`/`ZE`
codes remain.

Legend: ✅ supported · ➖ not applicable to engine · ❌ deliberately not tracked

---

## 0. File overview

| | T4 (W@W / Pluto T4) | T5 (BO1 / Pluto T5) | T6 (BO2 / Pluto T6) | T7 (BO3 / T7x AlterWare) |
|---|---|---|---|---|
| Source file | `_zm_stats_t4.gsc` | `_zm_stats_t5.gsc` | `_zm_stats_t6.gsc` | `_zm_stats_t7.gsc` |
| Lines | 1,833 | 2,117 | 2,390 | 2,405 |
| Compile step | ➖ (interpreted) | ➖ | ➖ | ✅ `_zm_stats_t7.compiled.gsc` via Cerberus |
| Helper script ships | ➖ | ➖ | ➖ | ➖ (dev-only helper lives outside repo) |
| Engine entry | `level thread Init()` | same | same | `REGISTER_SYSTEM("zombie_stats", &__init__, undefined)` |
| Module import | `maps\_zombiemode_utility` | `maps\_zombiemode_utility` | `maps\mp\zombies\_zm_utility` | `#using scripts\shared\...` BO3 system |
| Function keyword | none (legacy GSC) | none | none | `function` |
| Notify hashes | named strings | named strings | named strings | `#"..."` hashed (BO3 EOL — frozen) |

---

## 1. Wire format (emitted GSE codes)

| Code | Meaning | T4 | T5 | T6 | T7 |
|---|---|---|---|---|---|
| `K` | Player kill / death | ✅ | ✅ | ✅ | ✅ |
| `D` | Player damage | ✅ | ✅ | ✅ | ✅ |
| `AD` | Actor (zombie) damage | ✅ | ✅ | ✅ | ✅ |
| `AK` | Actor killed | ✅ | ✅ | ✅ | ✅ |
| `RD` | Per-player round data | ✅ | ✅ | ✅ | ✅ |
| `RC` | Round complete | ✅ | ✅ | ✅ | ✅ |
| `ZP` | Per-player zone/economy event | ✅ | ✅ | ✅ | ✅ |
| `ZW` | World/round event | ✅ | ✅ | ✅ | ✅ |

`RD` payload byte-identical across all four:
`<playerInfo>;<totalScore>;<currentScore>;<round>;<isGameOver>`.
Kills / downs / revives / damage are derived server-side from the AK/AD/K/D
stream — never pre-aggregated in GSC.

---

## 2. Match lifecycle

| | T4 | T5 | T6 | T7 |
|---|---|---|---|---|
| Match-ID dvar (`sv_iw4m_zm_matchid`) | ✅ | ✅ | ✅ | ✅ |
| Round dvar (`sv_iw4m_zm_round`) | ✅ | ✅ | ✅ | ✅ |
| Random match-ID seed (~10¹² collision space) | ✅ | ✅ | ✅ | ✅ |
| Intermission emit (`isGameOver=1`) | ✅ | ✅ | ✅ | ✅ |
| Concurrent K/RD drain wait (0.1s) | ✅ | ✅ | ✅ | ✅ |
| ExitLevel / fast-restart cleanup | implicit via endon | same | same | same |

Match-ID seed uses `randomint(1000000)+"_"+randomint(1000000)` not `gettime()`
because the engine clock returns 0 at init on T4/T5/T6.

---

## 3. Round events

| | T4 | T5 | T6 | T7 |
|---|---|---|---|---|
| `RD` per player per round | ✅ | ✅ | ✅ | ✅ |
| `RC;<round>` | ✅ | ✅ | ✅ | ✅ |
| Round-special detection (`ZW;round_special;<round>;<type>`) | ✅ dog only | ✅ dog/monkey/thief | ✅ dog/leaper | ✅ dog/monkey/wasp/spider/robot/quad/boss/ee |

**Special-round flag sources per engine:**

| Type | Flag | T4 | T5 | T6 | T7 |
|---|---|---|---|---|---|
| dog | `dog_round` | ✅ | ✅ | ✅ | ✅ |
| monkey | `monkey_round` | ➖ | ✅ (Ascension) | ➖ | ✅ |
| thief | `thief_round` | ➖ | ✅ (Five) | ➖ | ➖ |
| leaper | `leaper_round` | ➖ | ➖ | ✅ (Die Rise) | ➖ |
| wasp | `wasp_round` | ➖ | ➖ | ➖ | ✅ |
| spider | `spiders_from_mars_round` | ➖ | ➖ | ➖ | ✅ |
| robot | `three_robot_round` | ➖ | ➖ | ➖ | ✅ |
| quad | `special_quad_round` | ➖ | ➖ | ➖ | ✅ |
| boss | `boss_round` | ➖ | ➖ | ➖ | ✅ |
| ee | `ee_round` | ➖ | ➖ | ➖ | ✅ |

All four guard with `IsDefined(level.flag[name])` — T4 lacks `flag_exists()`,
others kept consistent for portability.

---

## 4. Zombies remaining

| | T4 | T5 | T6 | T7 |
|---|---|---|---|---|
| `ZW;zombies;<round>;<remaining>;<alive>` 5s change-gated poll | ✅ | ✅ | ✅ | ✅ |

Source: `level.zombie_total` + `get_enemy_count()` (T4/T5/T6) or BO3 equivalent.

---

## 5. Player economy events (`GSE;ZP;<info>;...`)

| Subtype | Format | T4 | T5 | T6 | T7 |
|---|---|---|---|---|---|
| Door buy | `door;buy;<name>;<cost>` | ✅ | ✅ | ✅ | ✅ |
| Weapon buy (wallbuy) | `weapon;buy;<weapon>;<cost>` | ✅ | ✅ | ✅ | ✅ |
| PaP upgrade | `weapon;upgrade;<weapon>` | ✅ | ✅ | ✅ | ✅ |
| PaP abandon (timeout/disc.) | `weapon;abandon;<weapon>` | ✅ | ✅ | ✅ | ✅ |
| Perk buy | `perk;buy;<perk>;<cost>` | ✅ (weapon-poll) | ✅ (notify) | ✅ (notify) | ✅ (notify) |
| Perk lost (QR-auto / Tombstone / Who's Who) | — | ❌ | ❌ | ❌ | ❌ |
| Mystery box outcome | `box;take\|pass\|teddy;...` | ✅ | ✅ | ✅ | ✅ |
| Box move | — | ❌ | ❌ | ❌ | ❌ |
| Power-up grab | `powerup;grab;<name>` | ✅ | ✅ | ✅ | ✅ |
| Trap activate | `trap;activate;<name>;<cost>` | ✅ electric | ✅ electric+turret | ✅ | ✅ |
| Buildable complete | `build;complete;<name>` | ➖ | ➖ | ✅ | ✅ (craftables) |
| Craftable complete | `build;complete;<name>` | ➖ | ➖ | ✅ | ✅ |
| Gobblegum activate | `gum;activate;<bgb>` | ➖ | ➖ | ➖ | ✅ |
| Gobblegum take (machine) | `gum;take;<bgb>;<cost>` | ➖ | ➖ | ➖ | ✅ |
| Gobblegum leave (refund-filter) | `gum;leave;<bgb>;<cost>` | ➖ | ➖ | ➖ | ✅ |
| Bank deposit | `bank;deposit;<amount>` | ➖ | ➖ | ✅ Tranzit/DieRise/Buried | ➖ |
| Bank withdraw | `bank;withdraw;<amount>` | ➖ | ➖ | ✅ Tranzit/DieRise/Buried | ➖ |
| Weapon locker store | `locker;store;<weapon>` | ➖ | ➖ | ✅ Tranzit/DieRise/Buried | ➖ |
| Weapon locker retrieve | `locker;retrieve;<weapon>` | ➖ | ➖ | ✅ Tranzit/DieRise/Buried | ➖ |
| Revive (co-op) | `revive;<reviverInfo>` | ✅ | ✅ | ✅ | ✅ |
| Self-revive (solo QR / Who's Who / Self Revive gum) | `revive;self` | ➖ (no self-revive in W@W) | ✅ | ✅ | ✅ |
| Down | `down` | ✅ | ✅ | ✅ | ✅ |
| Zombified | `zombified` | ✅ | ✅ | ✅ | ✅ |

Box detection: notify-driven with 3-tier user resolution + scoped teddy
suppression. PaP detection: lock-first attribution (`WatchPapTakenFlag` /
`WatchPapTimeoutFlag` / `WatchPapTriggerForBuyer` / `VerifyPapBuyerLock` /
`WatchPapOutcome` / `WatchPapDisconnectFlag`). All four engines have full
disconnect-cleanup coverage; T4/T5 synthesise the `pap_player_disconnected`
notify via the `WatchPapBuyerDisconnect` helper since their engines don't
emit it natively (T6/T7 do).

Perk detection diverges per engine:

| Engine | Mechanism | Why |
|---|---|---|
| T4 | Weapon-switch substring `IsSubStr(weapon, "zombie_perk")` | T4 has no `perk_bought` notify |
| T5 | `perk_bought` notify | Override-safe vs Ascension/Shangri-La `monkey_perk_bought` |
| T6 | `perk_bought` notify | Override-safe vs Die Rise achievement hook |
| T7 | `#"perk_bought"` notify | Standard BO3 path |

Power-up: proximity-poll on `script_model` entities with `powerup_name` set.
T4 explicitly does NOT hook `level.zombie_powerup_grab_func` — hooking it
disables the effect.

---

## 6. World events (`GSE;ZW;...`)

| Subtype | T4 | T5 | T6 | T7 |
|---|---|---|---|---|
| `power;on;world` | ✅ | ✅ | ✅ | ✅ |
| `power;on;player;<info>` | ✅ | ✅ | ✅ | ✅ |
| `power;off;world` | ➖ (W@W maps never power off) | ✅ | ✅ Tranzit | ✅ |
| `round_special;<round>;<type>` | ✅ | ✅ | ✅ | ✅ |
| `zombies;<round>;<remaining>;<alive>` | ✅ | ✅ | ✅ | ✅ |
| `easter_egg;step;<key>` | ✅ | ✅ | ✅ | ✅ |
| `easter_egg;complete;<map>` | ✅ canonical | ✅ canonical | ✅ canonical | ✅ derived |

Two equally-valid signal channels for the same outcome (`EasterEggOccurredAt`
gets set either way). Per-quest `HasCanonicalNotify` flag in
`MapEasterEggConfig.cs` decides which the server expects:

- **Canonical** (T4/T5/T6 + any T7 quest with a named terminal notify):
  GSC waits on the engine's terminal flag/notify and emits
  `easter_egg;complete;<map>`. Server marks complete on receipt.
- **Derived** (every T7 quest currently): GSC emits only `easter_egg;step`
  for each configured step. Server marks complete when all steps for the
  quest have logged.

T7 uses derivation because most BO3 main-quest terminal flags are hashed
in the shiversoftdev dump — a named-notify wait isn't always available.
Step-based derivation covers all 14 T7 maps using per-step flags that DO
have source names. Cases where T7 has a clean terminal flag (e.g.
`zm_zod` `ee_complete`) appear as the final step in the quest's step
list, so derivation still picks them up.

---

## 7. Easter Egg coverage (per map)

### T4 (Pluto W@W)

| Map | Main quest | Song EE | Notes |
|---|---|---|---|
| Nacht der Untoten | ❌ | ❌ | Pluto T4 entity hook broken |
| Verrückt | ❌ | ✅ song step (`level.eggs`) | |
| Shi No Numa | ❌ | ✅ song step (`level.eggs`) | |
| Der Riese | ✅ steps + flytrap + 3 meteors | ❌ | No canonical "complete" |

### T5 (Pluto BO1)

| Map | Main quest | Song EE | Notes |
|---|---|---|---|
| Kino der Toten | ❌ | ✅ song (shared `HookT5SongTriggers`) | |
| Five | ❌ | ✅ song | |
| Ascension | ✅ complete (`HookAscensionCasimir`) | ✅ | Teddy bears + Casimir flag |
| Call of the Dead | ✅ complete | ✅ ensemble (`HookCallOfDeadEnsemble`) | |
| Shangri-La | ✅ complete | ✅ (`HookShangriLaSidequest`) | |
| Moon | ✅ complete (`HookMoonRichtofen`) | ✅ | |

### T6 (Pluto BO2)

| Map | Main quest | Song EE | Map-specific |
|---|---|---|---|
| Tranzit | ✅ Maxis/Rich branching | ✅ song bears | Gramophone placement |
| Nuketown Zombies | — | ✅ meteor counter | Bears |
| Die Rise | ✅ Maxis/Rich + terminal + bears | ✅ | |
| Mob of the Dead | ✅ (`pop_goes_the_weasel_achieved`) | ✅ Rusty Cage / Nixie 115+935 | Multi-stage Pop Goes the Weasel |
| Buried | ✅ stages + terminals + bears | ✅ | Wisp stage |
| Origins | ✅ (`tomb_sidequest_complete`) | ✅ 3 counter songs + Little Lost Girl | 4 staffs |

### T7 (T7x BO3)

14/14 maps with EE step coverage. `easter_egg;complete` is a deliberate stub
in `WaitForEasterEggComplete` — terminal notify hooks live in
`WaitForT7EasterEggSteps` and completion derives from the last step server-side.

| Map (engine name) | Main | Song | Side / upgrade quests |
|---|---|---|---|
| `zm_zod` (SoE) | ✅ 8 main-quest flags | ✅ 3 song states | Arnie upgrade (hashed `#"hash_21edb6b6"`), Shield, Bouncing Bettys; ❌ Apothicon Sword per-character (deferred) |
| `zm_factory` (The Giant) | ✅ | ✅ | Flytrap (3-target) + secret-perk pad |
| `zm_castle` (Der Eisendrache) | ✅ 8 steps | ✅ 2 songs + music box + disco + Dead Again (3 bears) + Requiem (3 gramophones) | 4 elemental bow upgrades (weapon-substring poll); Storm ritual sub-steps `lit/wallrun/batteries/electrify` tracked; Wolf/Fire/Void ritual flags hashed → only upgrade tracked; Keeper / Wrath bow (nested EeStep tree) |
| `zm_island` (Zetsubou) | ✅ | ✅ | KT-4 base+upgrade + 4 skull rituals + 3 spider EE |
| `zm_stalingrad` (Gorod Krovi) | ✅ 6 steps | ✅ 3-song state polls | 5-step Dragon Gauntlet quest (`gauntlet_step_2/3/4/complete`) |
| `zm_genesis` (Revelations) | ✅ 13 steps | ✅ | Li'l Arnie prereq+done |
| `zm_tomb` (Origins) | ✅ | ✅ | 4 staff upgrades |
| `zm_cosmodrome` (Ascension) | ✅ | ✅ | |
| `zm_theater` (Kino) | ✅ | ✅ | |
| `zm_temple` (Shangri-La) | ✅ | ✅ | |
| `zm_moon` (Moon) | ✅ | ✅ | |
| `zm_prototype` (Nacht) | ✅ HnS `snd_zhdegg_activate` | ➖ | |
| `zm_asylum` (Verrückt) | ✅ HnS | ➖ | |
| `zm_sumpf` (Shi No Numa) | ✅ HnS | ✅ | |

Shared T7 helpers: `WatchT7FlagStep`, `WatchT7MusicStateStep`,
`WatchWeaponSubstringUpgrade` (generic weapon-inventory substring poller —
covers DE bows, Origins staffs, SoE shield/Bettys), `WaitForT7FlagInit`,
`PlayerHasWeaponSubstr`, `WatchT7SongEntityActivated` / `WaitForBActivatedThenEmit`
(per-trigger song detection — polls `self.b_activated` at 0.5s, no `self endon("death")`
so transient entity-death notifies can't kill the watcher),
`HookScriptOriginAtStruct` (polls until `end_game` for lazy-spawned `script_origin`
entities — Castle bears can spawn >20min after match start),
`WatchT7BeaconsLitAll` + `WaitForBeaconActivatedNotify` + `WaitForEntArrayByNoteworthy`
(counted-notify aggregator with per-call-site state via `level.zm_ee_lit_count[stepKey]`
map — used for Storm Bow's "light all beacons" ritual phase).

---

## 8. Map-specific mechanics

| Mechanic | T4 | T5 | T6 | T7 |
|---|---|---|---|---|
| Flytrap (Der Riese / Factory) | ✅ panel | ➖ | ➖ | ✅ 3-target |
| Meteor counter songs | ✅ 3 | ✅ (`WatchT5MeteorCounterSong`) | ✅ Nuketown / Origins | ➖ |
| Auto-turret trap | ➖ | ✅ Ascension PaP turrets | ➖ | ➖ |
| Buildables (`_zm_buildables`) | ➖ | ➖ | ✅ | ➖ |
| Craftables (`_zm_craftables`) | ➖ | ➖ | ✅ | ✅ |
| Bank deposit/withdraw | ➖ | ➖ | ✅ Tranzit/DieRise/Buried | ➖ |
| Weapon locker store/retrieve | ➖ | ➖ | ✅ Tranzit/DieRise/Buried | ➖ |
| Wonder-weapon upgrade attribution | ➖ | ➖ | Origins staffs (PaP path) | DE bows, GK gauntlet, SoE shield/Bettys/Arnie, Zetsubou KT-4 |
| Music-state polling | ➖ | ➖ | ➖ | ✅ |
| Hashed terminal-notify EE | ➖ | ➖ | ➖ | ✅ (SoE, GK) |
| Gobblegum machines | ➖ | ➖ | ➖ | ✅ |

---

## 9. Player connection

| | T4 | T5 | T6 | T7 |
|---|---|---|---|---|
| `level "connecting"` wait | ✅ | ✅ | ✅ | ✅ (`#"connecting"`) |
| Per-player watchers threaded | revive, zombified, perk | + perk_bought | + perk_bought | + perk_bought, gum activate |
| Explicit disconnect emission | ❌ (endon) | ❌ | ❌ | ❌ |

---

## 10. Deliberately not tracked (any engine)

- Perk loss (Quick Revive auto / Tombstone / Who's Who) — downstream classification only
- Permaperks (T6 Tranzit-line persistent buffs) — out of scope
- Tactical/lethal equipment (claymores, grenades, monkey bombs, EMPs)
- Box move location changes (detected internally for teddy-suppression scope, never emitted)
- T4 self-revive — W@W has no self-revive mechanic; reviver always != self

---

## 11. Known gaps

| Gap | Engine | Notes |
|---|---|---|
| Nacht der Untoten EE | T4 | Pluto T4 entity hook broken |
| T4 perk-buy poll | T4 | No `perk_bought` notify exists; weapon-switch poll is only path. Edge case: perk bought + downed within 0.1s tick drops the emission |
| Apothicon Sword per-character | T7 (SoE) | Hashed flags, deferred (4 separate quests) |
| Castle Wolf/Fire/Void ritual sub-steps | T7 (zm_castle) | Per-element ritual flags are hashed — only Storm has string-named `elemental_storm_*` flags. Per-entity scanner would be required for parity (~73 hashed flags); deferred |
| Live-test 12/14 maps | T7 | Only zm_factory + zm_sumpf live-verified |
| Live-test bank/locker | T6 | New emission paths added; need Tranzit/Die Rise/Buried verification |
| Gobblegum C# downstream | T7 | Events emitted, no premium handlers yet |

---

## 12. Compile / deploy

Only T7 needs compilation:

- Source: `_zm_stats_t7.gsc`
- Compiled: `_zm_stats_t7.compiled.gsc` (~49 KB; double-extension passes T7x's `filename.endsWith(".gsc")` suffix gate and disambiguates from the source filename)
- Magic bytes: `80 47 53 43 0d 0a` (`ÇGSC\r\n`)
- Toolchain: `linker_modtools.exe` + `Cerberus.CLI.exe` (PowerShell only — DLL search)
- Known benign noise: T7x logs `[DB] Error: Could not find scriptparsetree "custom_scripts/..."` on every custom_scripts/ load — script still executes correctly (verified by event flow in `games_zm.log`). Believed to be a secondary DB asset registry lookup running after the primary runtime load succeeded. No known suppression.
- See [`README.md`](README.md) for full build chain + gotchas, and [`t7-gsc-compile-chain.md`](../../../../.claude/projects/C--Users-Amos-RiderProjects--Cloned-IW4MAdmin/memory/t7-gsc-compile-chain.md)

T4/T5/T6 are interpreted by Pluto runtime directly — drop the `.gsc` in the
appropriate `scripts/zm/` (or game equivalent) and reload.
