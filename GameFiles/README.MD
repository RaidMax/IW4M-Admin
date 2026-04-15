# Game Files

GSC scripts deployed to Plutonium game servers for IW4MAdmin integration. Three systems:

## Game Interface

Core integration layer between IW4MAdmin and GSC. Required for all IW4MAdmin functionality beyond basic RCon.

**Shared files** (deployed to all games):
- `_integration_base.gsc` — core event loop, command processing, player connect/disconnect hooks
- `_integration_shared.gsc` — shared utilities, latency probe (`MonitorLatencyProbe` for measuring log pipeline latency)
- `_integration_utility.gsh` — helper macros and constants

**Game-specific files:**
| File | Game | Mode |
|------|------|------|
| `_integration_t4.gsc` | World at War (T4) | MP |
| `_integration_t4zm.gsc` | World at War (T4) | ZM/SP |
| `_integration_t5.gsc` | Black Ops (T5) | MP |
| `_integration_t5zm.gsc` | Black Ops (T5) | ZM |
| `_integration_t6.gsc` | Black Ops II (T6) | MP + ZM |
| `_integration_t6zm_helper.gsc` | Black Ops II (T6) | ZM helper |
| `_integration_iw4x.gsc` | Modern Warfare 2 (IW4x) | MP |
| `_integration_iw5.gsc` | Modern Warfare 3 (IW5) | MP |
| `_integration_iw6.gsc` | Ghosts (IW6) | MP |
| `_integration_t6_file_bus.gsc` | Black Ops II (T6) | File bus transport |
| `example_module.gsc` | — | Template for custom modules |

Full documentation: [GameInterface Wiki](https://github.com/RaidMax/IW4M-Admin/wiki/GameInterface)

## Zombie Stats

GSC scripts that emit zombie-specific telemetry (kills, downs, revives, points, rounds, power-ups, box hits, Pack-a-Punch usage) to IW4MAdmin via the Game Interface event bus. These feed the ZombieStats plugin for stat tracking, leaderboards, and match history.

| File | Game |
|------|------|
| `ZombieStats/_zm_stats_t4.gsc` | World at War (T4) |
| `ZombieStats/_zm_stats_t5.gsc` | Black Ops (T5) |
| `ZombieStats/_zm_stats_t6.gsc` | Black Ops II (T6) |

## AntiCheat

Custom callback scripts for server-side anti-cheat. Game-specific:

| Directory | Game |
|-----------|------|
| `AntiCheat/IW4x/` | Modern Warfare 2 (IW4x) |
| `AntiCheat/IW5/` | Modern Warfare 3 (IW5) |
| `AntiCheat/PT6/` | Black Ops II (Pluto T6) |

## Installation

Run `deploy.bat` to copy all scripts to Plutonium's local storage directories. Requires Plutonium to be installed at the default `%LOCALAPPDATA%\Plutonium` path.

Currently deploys to: **T4**, **IW5**, **T5**, **T6**.

### Manual Installation

Copy files to your Plutonium storage scripts directory. Path varies by game:

| Game | Base Path |
|------|-----------|
| T4 (WaW) | `%LOCALAPPDATA%\Plutonium\storage\t4\raw\scripts\` |
| T5 (BO1) | `%LOCALAPPDATA%\Plutonium\storage\t5\scripts\` |
| T6 (BO2) | `%LOCALAPPDATA%\Plutonium\storage\t6\scripts\` |
| IW5 (MW3) | `%LOCALAPPDATA%\Plutonium\storage\iw5\scripts\` |

Place shared Game Interface files in the base scripts directory. Game-specific MP scripts go in the `mp/` subdirectory. ZM scripts go in the appropriate ZM subdirectory (`sp/` for T4, `sp/zom/` for T5, `zm/` for T6).
