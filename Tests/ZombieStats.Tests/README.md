# Zombies Stats tests

From the public repository root, with the .NET 10 SDK:

```powershell
dotnet test Tests/ZombieStats.Tests/ZombieStats.Tests.csproj -c Prerelease
```

`Prerelease` exercises the normal plugin assemblies without Debug's host setup scripts or
Release obfuscation. Tests use disposable SQLite databases, fake servers and captured
enhancer calls. They never connect to game servers or production databases. The existing
console regression harness remains available; its lifecycle checks are also discoverable
through this test project.

Collect line and branch coverage:

```powershell
dotnet test Tests/ZombieStats.Tests/ZombieStats.Tests.csproj -c Prerelease --collect:"XPlat Code Coverage" --settings Tests/zombie-stats.runsettings --results-directory Tests/TestResults/public
```

Coverage is scoped to the zombie plugin, zombie entities and the host event parser. It
does not claim coverage of the broader IW4MAdmin project. GitHub Actions runs this suite
on relevant pull requests and on the Zombies Stats branch and uploads TRX/Cobertura files.

For a combined per-file line summary, pass the Cobertura files from the public and private
runs to `Tests/Show-ZombieCoverage.ps1 -CoverageFiles <public.xml>,<premium.xml> -OutputPath Tests/TestResults/coverage.md`.
Pass only the reports you intend to compare; do not include old run directories. Async
state machines and lambdas remain included in the measurements.

The tests protect the GSC wire contract (all event families, malformed input, GUID styles,
revive attribution, clock provenance), free-to-Premium dispatch, Stats bridging, stale
gametype fallback, roster reconciliation, lifecycle flushing, SQLite migrations and
aggregate uniqueness. Test cases assert observable behavior and database results.

The proprietary companion suite lives in `_Private/Tests/ZombieStatsPremium.Tests` and
imports the same test packages and coverage settings. It is deliberately absent from the
public solution and public CI checkout. See that project's README for its command and
additional coverage.

Process-wide host subscriptions mean these fixtures run sequentially. Coverage excludes
generated code and migrations from the percentage denominator; migrations are exercised
against a real SQLite database. GSC compilation and game-engine behavior still require
the existing compiler checks and game-server verification.
