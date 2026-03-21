# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What is IW4MAdmin

IW4MAdmin is a game server administration tool for Call of Duty dedicated servers (IW4x, IW6x, Pluto T6/IW5, CoD4x, TeknoMW3, etc.). It monitors server activity, manages players (bans/kicks/warnings), tracks stats, and provides a web interface for administration.

## Build Commands

```bash
# Restore packages
dotnet restore IW4MAdmin.sln

# Build the full solution (Debug)
dotnet build IW4MAdmin.sln

# Build the main application
dotnet build Application/Application.csproj

# Build a specific plugin
dotnet build Plugins/Stats/Stats.csproj

# Publish the application (Prerelease config used in CI)
dotnet publish Application/Application.csproj -c Prerelease -o Publish/Prerelease

# Build all plugins (from repo root)
find Plugins -name "*.csproj" -exec dotnet publish {} -c Debug -o BUILD/Plugins \;
```

Build configurations: `Debug`, `Release`, `Prerelease`. Target framework: `net10.0`.

In Debug mode, a PreBuild PowerShell script runs and `PluginDebugReference` is included (it aggregates all plugin projects for easier debugging).

## Frontend Build

The web frontend requires additional steps beyond `dotnet build`:

1. **Tailwind CSS**: `tailwindcss -i wwwroot/css/src/app.css -o wwwroot/css/app.css --minify` (run from WebfrontCore/)
2. **SCSS**: Compiled via `Excubo.WebCompiler` (`webcompiler -r WebfrontCore/wwwroot/css/src -o WebfrontCore/wwwroot/css/`)
3. **JS bundling**: Uses `dotnet-bundle` with `WebfrontCore/bundleconfig.json`
4. **Phosphor icon fonts**: CSS paths are patched post-build (see CI workflow)

## Tests

Tests are in `Tests/ApplicationTests/` but are **not** included in the solution file. They use NUnit with FakeItEasy/Moq and FluentAssertions. Test fixtures and mock data are in `Tests/ApplicationTests/Fixtures/` and `Tests/ApplicationTests/Mocks/`.

## Database & Migrations

EF Core 9.0 with three supported providers (each has its own migration context in `Data/MigrationContext/`):
- **SQLite** — default, `SqliteDatabaseContext`
- **PostgreSQL** — `PostgresqlDatabaseContext` (via Npgsql)
- **MySQL** — `MySqlDatabaseContext` (via Pomelo)

Migrations live in `Data/Migrations/{Sqlite,Postgresql,MySql}/`. When adding a migration, you must add it for each provider using the corresponding context class.

## Architecture

### Project Dependency Graph

```
Application (entry point, console host)
├── SharedLibraryCore (interfaces, models, commands, base classes)
│   └── Data (EF Core contexts, entities, migrations)
├── WebfrontCore (Blazor Server UI, API controllers)
│   └── WebCommon (shared Blazor UI components)
├── Integrations.Cod (Call of Duty RCon protocol, game parsers)
├── Integrations.Source (Source engine integration)
└── Plugins/* (compiled plugins, loaded dynamically)
```

### Key Patterns

**ApplicationManager** (`Application/ApplicationManager.cs`): Central orchestrator. Manages game server instances, dispatches events, coordinates command execution, and manages plugin lifecycles.

**Event System**: Event-driven via `IGameServerEventSubscriptions`, `IManagementEventSubscriptions`, and `IGameEventSubscriptions`. Game servers emit events (player connects, chat messages, kills) which are processed by handlers and plugins.

**Plugin System — two generations**:
- **IPluginV2** (modern): Registers DI dependencies via `RegisterDependencies(IServiceCollection)`, subscribes to events. Used by Stats, Mute, etc.
- **IPlugin** (legacy): Older interface, still supported.
- **Script plugins**: JavaScript (via Jint engine) and C# scripts in `Plugins/ScriptPlugins/`. These are the game-specific parser plugins (e.g., `ParserIW4x.js`, `ParserPlutoniumT6.js`).

Plugin DLLs are loaded dynamically at runtime by `Application/Plugin/PluginImporter.cs`.

**Command System**: Commands extend `SharedLibraryCore.Command`. Built-in commands in `Application/Commands/` and `SharedLibraryCore/Commands/`. Commands are permission-gated by `EFClient.Permission` level.

**Web Layer**: ASP.NET Core with Blazor Server. Components in `WebfrontCore/Components/`. REST API controllers in `WebfrontCore/Controllers/API/`. Auth uses cookies + JWT + optional TOTP 2FA. Real-time updates via SignalR.

**RCon Communication**: Game servers are controlled via Remote Console (RCon) protocol. Parsers in `Application/RConParsers/` handle game-specific response formats. `Integrations.Cod` implements the CoD-specific RCon wire protocol.

### Key Entities (Data Layer)

- `EFClient` — game players
- `EFAlias` / `EFAliasLink` — player name/IP history
- `EFPenalty` — bans, kicks, warnings, mutes
- `EFServer` — game server metadata
- `EFClientKill` / `EFClientMessage` — stats tracking
- `EFMeta` / `EFChangeHistory` — metadata and audit trail

### Localization

Multi-language support (en-US, ru-RU, es-EC, pt-BR, de-DE). Translation files are downloaded at build time to `Localization/` directory.
