param ( [string]$SolutionDir, [string]$OutputDir )

if (-not (Test-Path "$SolutionDir/WebfrontCore/wwwroot/font")) {
    Write-Output "restoring web dependencies"
    dotnet tool install Microsoft.Web.LibraryManager.Cli --global
    Set-Location "$SolutionDir/WebfrontCore"
    libman restore
    Set-Location $SolutionDir
    Copy-Item -Recurse -Force -Path "$SolutionDir/WebfrontCore/wwwroot/lib/open-iconic/font/fonts" "$SolutionDir/WebfrontCore/wwwroot/font"
}

if (-not (Test-Path "$SolutionDir/WebfrontCore/wwwroot/lib/open-iconic/font/css/open-iconic-bootstrap-override.scss")) {
    Write-Output "load external resources"
    New-Item -ItemType Directory -Force -Path "$SolutionDir/WebfrontCore/wwwroot/lib/open-iconic/font/css"
    Invoke-WebRequest -Uri "https://raw.githubusercontent.com/iconic/open-iconic/master/font/css/open-iconic-bootstrap.scss" -OutFile "$SolutionDir/WebfrontCore/wwwroot/lib/open-iconic/font/css/open-iconic-bootstrap-override.scss"
    (Get-Content "$SolutionDir/WebfrontCore/wwwroot/lib/open-iconic/font/css/open-iconic-bootstrap-override.scss") -replace '../fonts/', '/font/' | Set-Content "$SolutionDir/WebfrontCore/wwwroot/lib/open-iconic/font/css/open-iconic-bootstrap-override.scss"
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
webcompiler "$SolutionDir/WebfrontCore/wwwroot/lib/open-iconic/font/css/open-iconic-bootstrap-override.scss" -o "$SolutionDir/WebfrontCore/wwwroot/css/" -m disable -z disable

if (-not (Test-Path "$SolutionDir/bundle/dotnet-bundle.dll")) {
    New-Item -ItemType Directory -Force -Path "$SolutionDir/bundle"
    Write-Output "getting dotnet bundle"
    Invoke-WebRequest -Uri "https://raidmax.org/IW4MAdmin/res/dotnet-bundle.zip" -OutFile "$SolutionDir/bundle/dotnet-bundle.zip"
    Write-Output "unzipping download"
    Expand-Archive -Path "$SolutionDir/bundle/dotnet-bundle.zip" -DestinationPath "$SolutionDir/bundle" -Force
}

Write-Output "executing dotnet-bundle"
Set-Location "$SolutionDir/bundle"
dotnet "dotnet-bundle.dll" clean "$SolutionDir/WebfrontCore/bundleconfig.json"
dotnet "dotnet-bundle.dll" "$SolutionDir/WebfrontCore/bundleconfig.json"
Set-Location $SolutionDir

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
Copy-Item -Recurse -Force -Path "$SolutionDir/BUILD/Plugins/*.dll" -Destination "$OutputDir/Plugins/"
Copy-Item -Recurse -Force -Path "$SolutionDir/Plugins/ScriptPlugins/*.js" -Destination "$OutputDir/Plugins/"
