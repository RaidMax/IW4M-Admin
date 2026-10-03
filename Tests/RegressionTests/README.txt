Run from the repository root:
  dotnet run --project Tests/RegressionTests/RegressionTests.csproj -c Release

Run against a publish directory AFTER Application/BuildScripts/PostBuild.sh:
  dotnet run --project Tests/RegressionTests/RegressionTests.csproj -c Release -- --package <publish-directory>

These checks use temporary SQLite databases. They cover migration upgrades and repairs,
provider model snapshots, category queries, proxy trust, bundle replacement, session
scores, and script compilation. Failures exit with a nonzero status.

Remote reverse proxies must be listed in Webfront.TrustedProxyAddresses, by IP or CIDR network.
Loopback proxies remain trusted by the framework default.

For a Debug project-only build, provide the solution root required by PreBuild.ps1:
  dotnet run --project Tests/RegressionTests/RegressionTests.csproj -c Debug -p:SolutionDir=<repository-root>/
A full solution build supplies SolutionDir automatically.

Additional checks verify patched native SQLite, the repaired snapshot server
index on fresh/upgrade paths, and API-document generation with patched OpenAPI.
The API smoke test binds a temporary loopback-only HTTP listener and stops it.

Zombies parser checks verify source clocks/metadata survive the second parsing
stage, distinguish absent clocks from zero, and preserve power attribution.
