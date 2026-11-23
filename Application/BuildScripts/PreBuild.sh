#!/bin/bash

SolutionDir="$1"
OutputDir="$2"
export PATH="$PATH:~/.dotnet/tools"

if [ ! -d "$SolutionDir/WebfrontCore/wwwroot/lib/font" ]; then
    echo restoring web dependencies
    dotnet tool install Microsoft.Web.LibraryManager.Cli --global
    cd "$SolutionDir/WebfrontCore" || exit
    libman restore
    cd "$SolutionDir" || exit
fi

echo "checking for Excubo.WebCompiler..."

toolExists=$(dotnet tool list -g | grep "Excubo.WebCompiler")

if [[ ! -z "$toolExists" ]]; then
  installedVersion=$(echo "$toolExists" | grep -oE "Excubo.WebCompiler\s+(\d+\.\d+\.\d+)" | grep -oE "\d+\.\d+\.\d+")
  latestVersion=$(curl -s "https://api.nuget.org/v3-flatcontainer/excubo.webcompiler/index.json" | jq -r '.versions | last')

  echo "installed version: $installedVersion"
  echo "latest version: $latestVersion"

  if [[ "$latestVersion" != "$installedVersion" && "$(printf '%s\n%s' "$installedVersion" "$latestVersion" | sort -V | head -n 1)" == "$installedVersion" ]]; then
    echo "updating Excubo.WebCompiler to version $latestVersion..."
    dotnet tool update Excubo.WebCompiler --global || echo "failed to update Excubo.WebCompiler. Using existing version."
  else
    echo "Excubo.WebCompiler is already up-to-date."
  fi
else
  echo "installing Excubo.WebCompiler..."
  dotnet tool install Excubo.WebCompiler --global
fi

echo "compiling scss files"

webcompiler -r "$SolutionDir/WebfrontCore/wwwroot/css/src" -o "$SolutionDir/WebfrontCore/wwwroot/css/" -m disable -z disable

if [ ! -f "$SolutionDir/bundle/dotnet-bundle.dll" ]; then
    mkdir -p "$SolutionDir/bundle"
    echo getting dotnet bundle
    curl -o "$SolutionDir/bundle/dotnet-bundle.zip" https://raidmax.org/IW4MAdmin/res/dotnet-bundle.zip
    echo unzipping download
    unzip "$SolutionDir/bundle/dotnet-bundle.zip" -d "$SolutionDir/bundle"
fi
echo executing dotnet-bundle
cd "$SolutionDir/bundle" || exit
dotnet dotnet-bundle.dll clean "$SolutionDir/WebfrontCore/bundleconfig.json"
dotnet dotnet-bundle.dll "$SolutionDir/WebfrontCore/bundleconfig.json"
cd "$SolutionDir" || exit

# BUILD/Plugins is only a staging area; start clean so files from older plugin builds don't linger
rm -rf "$SolutionDir/BUILD/Plugins"
mkdir -p "$SolutionDir/BUILD/Plugins"
echo building plugins
cd "$SolutionDir/Plugins" || exit 
find . -name "*.csproj" -print0 | xargs -0 -I {} dotnet publish {} -o "$SolutionDir/BUILD/Plugins" --no-restore
cd "$SolutionDir" || exit

if [ ! -d "$OutputDir/Localization" ]; then
    echo downloading translations
    mkdir -p "$OutputDir/Localization"
    localizations=("en-US" "ru-RU" "es-EC" "pt-BR" "de-DE")
    for localization in "${localizations[@]}"
    do
        url="https://master.iw4.zip/localization/$localization"
        filePath="$OutputDir/Localization/IW4MAdmin.$localization.json"
        curl -s "$url" -o "$filePath"
    done
fi

echo copying plugins to build dir
mkdir -p "$OutputDir/Plugins"

# The plugin importer loads every dll in Plugins, so stale copies of libraries can shadow the host's versions.
# Remove dlls this script copied last time (tracked in a manifest) and any dll the host already ships,
# but leave other files alone (e.g. third-party plugins dropped in by hand).
pluginManifest="$OutputDir/Plugins/.prebuild-plugins.txt"
pluginDllNames=()
for dll in "$SolutionDir"/BUILD/Plugins/*.dll; do
    [ -f "$dll" ] && pluginDllNames+=("$(basename "$dll")")
done

staleDlls=()
if [ -f "$pluginManifest" ]; then
    while IFS= read -r name || [ -n "$name" ]; do
        name="${name%$'\r'}"
        [ -n "$name" ] && staleDlls+=("$name")
    done < "$pluginManifest"
fi
for dll in "$OutputDir"/Plugins/*.dll; do
    [ -f "$dll" ] || continue
    name="$(basename "$dll")"
    [ -f "$OutputDir/$name" ] && staleDlls+=("$name")
done

if [ ${#staleDlls[@]} -gt 0 ]; then
    printf '%s\n' "${staleDlls[@]}" | sort -u | while IFS= read -r name; do
        case "$name" in */*|..|.) continue ;; esac
        if [ ${#pluginDllNames[@]} -gt 0 ] && printf '%s\n' "${pluginDllNames[@]}" | grep -qxF -- "$name"; then
            continue
        fi
        if [ -f "$OutputDir/Plugins/$name" ]; then
            echo "removing stale plugin file $name"
            rm -f -- "$OutputDir/Plugins/$name"
        fi
    done
fi

if [ ${#pluginDllNames[@]} -gt 0 ]; then
    cp -f "$SolutionDir"/BUILD/Plugins/*.dll "$OutputDir/Plugins/"
    printf '%s\n' "${pluginDllNames[@]}" > "$pluginManifest"
else
    : > "$pluginManifest"
fi
cp -f "$SolutionDir"/Plugins/ScriptPlugins/*.js "$OutputDir/Plugins/" 2>/dev/null || true
cp -f "$SolutionDir"/Plugins/ScriptPlugins/*.cs "$OutputDir/Plugins/" 2>/dev/null || true
