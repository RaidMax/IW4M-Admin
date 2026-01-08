#!/bin/bash

PublishDir="$1"
SourceDir="$2"

if [ -z "$PublishDir" ] || [ -z "$SourceDir" ]; then
    echo "Usage: $0 <PublishDir> <SourceDir>"
    exit 1
fi

echo "Deleting extra runtime files"
declare -a runtimes=("linux-arm" "linux-arm64" "linux-armel" "osx" "osx-x64" "win-arm" "win-arm64" "alpine-x64" "linux-musl-x64")
for runtime in "${runtimes[@]}"; do
    if [ -d "$PublishDir/runtimes/$runtime" ]; then
        rm -rf "$PublishDir/runtimes/$runtime"
    fi
done

echo "Deleting misc files"
if [ -f "$PublishDir/web.config" ]; then rm "$PublishDir/web.config"; fi
if [ -f "$PublishDir/libman.json" ]; then rm "$PublishDir/libman.json"; fi
rm -f "$PublishDir"/*.exe
rm -f "$PublishDir"/*.pdb
rm -f "$PublishDir"/IW4MAdmin
rm -f "$PublishDir"/*.xml

echo "Setting up default folders"
mkdir -p "$PublishDir/Plugins"
mkdir -p "$PublishDir/Configuration"
mv "$PublishDir/DefaultSettings.json" "$PublishDir/Configuration/"

mkdir -p "$PublishDir/Lib"
rm -f "$PublishDir/Microsoft.CodeAnalysis*.dll"

# Get list of plugin DLLs from BUILD/Plugins (dynamically detected)
pluginDllNames=()
if [ -d "$SourceDir/BUILD/Plugins" ]; then
    for pluginDll in "$SourceDir/BUILD/Plugins"/*.dll; do
        if [ -f "$pluginDll" ]; then
            pluginDllNames+=("$(basename "$pluginDll")")
        fi
    done
fi

# Move DLLs to Lib, excluding plugin assemblies (they go to Plugins/ folder)
# Exception: Stats.dll should go to Lib as it's tightly integrated with the application
for dll in "$PublishDir"/*.dll; do
    if [ ! -f "$dll" ]; then
        continue
    fi
    dllName=$(basename "$dll")
    
    # Stats.dll is an exception - it goes to Lib, not Plugins
    if [ "$dllName" = "Stats.dll" ]; then
        mv "$dll" "$PublishDir/Lib/"
        continue
    fi
    
    isPlugin=false
    for plugin in "${pluginDllNames[@]}"; do
        if [ "$dllName" = "$plugin" ]; then
            isPlugin=true
            break
        fi
    done
    if [ "$isPlugin" = false ]; then
        mv "$dll" "$PublishDir/Lib/"
    else
        rm -f "$dll"  # Remove from publish dir, plugins are copied from BUILD/Plugins
    fi
done
mv "$PublishDir"/*.json "$PublishDir/Lib/"
mv "$PublishDir/runtimes" "$PublishDir/Lib/runtimes"
mv "$PublishDir/ru" "$PublishDir/Lib/ru"
mv "$PublishDir/de" "$PublishDir/Lib/de"
mv "$PublishDir/pt" "$PublishDir/Lib/pt"
mv "$PublishDir/es" "$PublishDir/Lib/es"
rm -rf "$PublishDir/cs"
rm -rf "$PublishDir/fr"
rm -rf "$PublishDir/it"
rm -rf "$PublishDir/ja"
rm -rf "$PublishDir/ko"
rm -rf "$PublishDir/pl"
rm -rf "$PublishDir/pt-BR"
rm -rf "$PublishDir/tr"
rm -rf "$PublishDir/zh-Hans"
rm -rf "$PublishDir/zh-Hant"
if [ -d "$PublishDir/refs" ]; then mv "$PublishDir/refs" "$PublishDir/Lib/refs"; fi
