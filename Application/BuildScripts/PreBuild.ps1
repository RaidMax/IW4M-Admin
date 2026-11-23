param ( [string]$SolutionDir, [string]$OutputDir )

if (-not (Test-Path "$SolutionDir/WebfrontCore/wwwroot/font")) {
    Write-Output "restoring web dependencies"
    dotnet tool install Microsoft.Web.LibraryManager.Cli --global
    Set-Location "$SolutionDir/WebfrontCore"
    libman restore
    Set-Location $SolutionDir
}

Write-Output "checking for Excubo.WebCompiler..."

$toolExists = dotnet tool list -g | Where-Object { $_ -match "Excubo.WebCompiler" }

if ($toolExists) {
  $installedVersion = ($toolExists | Select-String -Pattern "Excubo.WebCompiler\s+(\d+\.\d+\.\d+)" -AllMatches).Matches.Groups[1].Value
  $latestVersion = (Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/excubo.webcompiler/index.json" | ConvertFrom-Json).versions | Select-Object -Last 1

  Write-Output "installed version: $installedVersion"
  Write-Output "latest version: $latestVersion"

  if ([version]$latestVersion -gt [version]$installedVersion) {
    Write-Output "updating Excubo.WebCompiler to version $latestVersion..."
    try {
      dotnet tool update Excubo.WebCompiler --global
    }
    catch {
      Write-Output "failed to update Excubo.WebCompiler. Using existing version."
    }
  } else {
    Write-Output "Excubo.WebCompiler is already up-to-date."
  }
} else {
  Write-Output "installing Excubo.WebCompiler..."
  dotnet tool install Excubo.WebCompiler --global
}

Write-Output "compiling scss files"

webcompiler -r "$SolutionDir/WebfrontCore/wwwroot/css/src" -o WebfrontCore/wwwroot/css/ -m disable -z disable

# bundleconfig.json (app.min.css / app.min.js) is bundled by WebfrontCore's own BundleWebAssets target, before its
# static web assets are resolved. Doing it here raced with WebfrontCore's build when projects built in parallel.

# BUILD/Plugins is only a staging area; start clean so files from older plugin builds don't linger
if (Test-Path "$SolutionDir/BUILD/Plugins") {
    Remove-Item -Recurse -Force -Path "$SolutionDir/BUILD/Plugins/*"
}
New-Item -ItemType Directory -Force -Path "$SolutionDir/BUILD/Plugins"
Write-Output "building plugins"
Set-Location "$SolutionDir/Plugins"
Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object { dotnet publish $_.FullName -o "$SolutionDir/BUILD/Plugins" --no-restore }
Set-Location $SolutionDir

if (-not (Test-Path "$OutputDir/Localization")) {
    Write-Output "downloading translations"
    New-Item -ItemType Directory -Force -Path "$OutputDir/Localization"
    $localizations = @("en-US", "ru-RU", "es-EC", "pt-BR", "de-DE")
    foreach ($localization in $localizations) {
        $url = "https://master.iw4.zip/localization/$localization"
        $filePath = "$OutputDir/Localization/IW4MAdmin.$localization.json"
        Invoke-WebRequest -Uri $url -OutFile $filePath -UseBasicParsing
    }
}

Write-Output "copying plugins to build dir"
New-Item -ItemType Directory -Force -Path "$OutputDir/Plugins"

# The plugin importer loads every dll in Plugins, so stale copies of libraries can shadow the host's versions.
# Remove dlls this script copied last time (tracked in a manifest) and any dll the host already ships,
# but leave other files alone (e.g. third-party plugins dropped in by hand).
$pluginManifest = "$OutputDir/Plugins/.prebuild-plugins.txt"
$pluginDlls = Get-ChildItem -Path "$SolutionDir/BUILD/Plugins" -Filter *.dll
$pluginDllNames = $pluginDlls | ForEach-Object { $_.Name }
$staleDlls = @()
if (Test-Path $pluginManifest) {
    $staleDlls += Get-Content $pluginManifest
}
$staleDlls += Get-ChildItem -Path "$OutputDir/Plugins" -Filter *.dll |
    Where-Object { Test-Path (Join-Path $OutputDir $_.Name) } |
    ForEach-Object { $_.Name }
$staleDlls | Sort-Object -Unique | Where-Object { $pluginDllNames -notcontains $_ } | ForEach-Object {
    $stalePath = Join-Path "$OutputDir/Plugins" $_
    if (Test-Path $stalePath) {
        Write-Output "removing stale plugin file $_"
        Remove-Item -Force -Path $stalePath
    }
}

$pluginDlls | Copy-Item -Force -Destination "$OutputDir/Plugins/"
$pluginDllNames | Set-Content -Path $pluginManifest
Copy-Item -Recurse -Force -Path "$SolutionDir/Plugins/ScriptPlugins/*.js" -Destination "$OutputDir/Plugins/" -ErrorAction SilentlyContinue
Copy-Item -Recurse -Force -Path "$SolutionDir/Plugins/ScriptPlugins/*.cs" -Destination "$OutputDir/Plugins/" -ErrorAction SilentlyContinue
