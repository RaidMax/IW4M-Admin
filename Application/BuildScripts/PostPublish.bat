set SolutionDir=%1
set ProjectDir=%2
set TargetDir=%3
set CurrentConfiguration=%4
SET COPYCMD=/Y

echo Deleting extra language files

if exist  "%SolutionDir%Publish\Windows\en-US\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\en-US'
if exist  "%SolutionDir%Publish\Windows\de\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\de'
if exist "%SolutionDir%Publish\Windows\es\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\es'
if exist "%SolutionDir%Publish\Windows\fr\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\fr'
if exist "%SolutionDir%Publish\Windows\it\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\it'
if exist "%SolutionDir%Publish\Windows\ja\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\ja'
if exist "%SolutionDir%Publish\Windows\ko\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\ko'
if exist "%SolutionDir%Publish\Windows\ru\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\ru'
if exist "%SolutionDir%Publish\Windows\zh-Hans\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\zh-Hans'
if exist "%SolutionDir%Publish\Windows\zh-Hant\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\zh-Hant'

if exist  "%SolutionDir%Publish\WindowsPrerelease\en-US\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\en-US'
if exist  "%SolutionDir%Publish\WindowsPrerelease\de\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\de'
if exist "%SolutionDir%Publish\WindowsPrerelease\es\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\es'
if exist "%SolutionDir%Publish\WindowsPrerelease\fr\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\fr'
if exist "%SolutionDir%Publish\WindowsPrerelease\it\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\it'
if exist "%SolutionDir%Publish\WindowsPrerelease\ja\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\ja'
if exist "%SolutionDir%Publish\WindowsPrerelease\ko\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\ko'
if exist "%SolutionDir%Publish\WindowsPrerelease\ru\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\ru'
if exist "%SolutionDir%Publish\WindowsPrerelease\zh-Hans\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\zh-Hans'
if exist "%SolutionDir%Publish\WindowsPrerelease\zh-Hant\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\zh-Hant'

echo Deleting extra runtime files
if exist "%SolutionDir%Publish\Windows\runtimes\linux-arm" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\linux-arm'
if exist "%SolutionDir%Publish\Windows\runtimes\linux-arm64" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\linux-arm64'
if exist "%SolutionDir%Publish\Windows\runtimes\linux-armel" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\linux-armel'

if exist "%SolutionDir%Publish\Windows\runtimes\osx" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\osx'
if exist "%SolutionDir%Publish\Windows\runtimes\osx-x64" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\osx-x64'

if exist "%SolutionDir%Publish\Windows\runtimes\win-arm" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\win-arm'
if exist "%SolutionDir%Publish\Windows\runtimes\win-arm64" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\win-arm64'

if exist "%SolutionDir%Publish\WindowsPrerelease\runtimes\linux-arm" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\runtimes\linux-arm'
if exist "%SolutionDir%Publish\WindowsPrerelease\runtimes\linux-arm64" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\runtimes\linux-arm64'
if exist "%SolutionDir%Publish\WindowsPrerelease\runtimes\linux-armel" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\runtimes\linux-armel'

if exist "%SolutionDir%Publish\WindowsPrerelease\runtimes\osx" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\runtimes\osx'
if exist "%SolutionDir%Publish\WindowsPrerelease\runtimes\osx-x64" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\runtimes\osx-x64'

if exist "%SolutionDir%Publish\WindowsPrerelease\runtimes\win-arm" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\runtimes\win-arm'
if exist "%SolutionDir%Publish\WindowsPrerelease\runtimes\win-arm64" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\WindowsPrerelease\runtimes\win-arm64'

echo Deleting misc files
if exist "%SolutionDir%Publish\Windows\web.config" del "%SolutionDir%Publish\Windows\web.config"
del "%SolutionDir%Publish\Windows\*pdb"

if exist "%SolutionDir%Publish\WindowsPrerelease\web.config" del "%SolutionDir%Publish\WindowsPrerelease\web.config"
del "%SolutionDir%Publish\WindowsPrerelease\*pdb"

echo setting up library folders

if "%CurrentConfiguration%" == "Prerelease" (
	echo PR-Config
	if not exist  "%SolutionDir%Publish\WindowsPrerelease\Configuration" md "%SolutionDir%Publish\WindowsPrerelease\Configuration"
	move "%SolutionDir%Publish\WindowsPrerelease\DefaultSettings.json" "%SolutionDir%Publish\WindowsPrerelease\Configuration\"
)

if "%CurrentConfiguration%" == "Release" (
	echo R-Config
	if not exist  "%SolutionDir%Publish\Windows\Configuration" md "%SolutionDir%Publish\Windows\Configuration"
	if exist "%SolutionDir%Publish\Windows\DefaultSettings.json" move "%SolutionDir%Publish\Windows\DefaultSettings.json" "%SolutionDir%Publish\Windows\Configuration\DefaultSettings.json"
)

if "%CurrentConfiguration%" == "Prerelease" (
	echo PR-LIB
	if not exist "%SolutionDir%Publish\WindowsPrerelease\Lib\" md "%SolutionDir%Publish\WindowsPrerelease\Lib\"
	move "%SolutionDir%Publish\WindowsPrerelease\*.dll" "%SolutionDir%Publish\WindowsPrerelease\Lib\"
	move "%SolutionDir%Publish\WindowsPrerelease\*.json" "%SolutionDir%Publish\WindowsPrerelease\Lib\"
)

if "%CurrentConfiguration%" == "Release" (
	echo R-LIB
	if not exist "%SolutionDir%Publish\Windows\Lib\" md "%SolutionDir%Publish\Windows\Lib\"
	move "%SolutionDir%Publish\Windows\*.dll" "%SolutionDir%Publish\Windows\Lib\"
	move "%SolutionDir%Publish\Windows\*.json" "%SolutionDir%Publish\Windows\Lib\"
)

if "%CurrentConfiguration%" == "Prerelease" (
	echo PR-RT
	move "%SolutionDir%Publish\WindowsPrerelease\runtimes" "%SolutionDir%Publish\WindowsPrerelease\Lib\runtimes"
	if exist "%SolutionDir%Publish\WindowsPrerelease\refs" move "%SolutionDir%Publish\WindowsPrerelease\refs" "%SolutionDir%Publish\WindowsPrerelease\Lib\refs"
)


if "%CurrentConfiguration%" == "Release" (
	echo R-RT
	move "%SolutionDir%Publish\Windows\runtimes" "%SolutionDir%Publish\Windows\Lib\runtimes"
	if exist "%SolutionDir%Publish\Windows\refs" move "%SolutionDir%Publish\Windows\refs" "%SolutionDir%Publish\Windows\Lib\refs"
)

if "%CurrentConfiguration%" == "Prerelease" (
	echo PR-LOC
	if not exist  "%SolutionDir%Publish\WindowsPrerelease\Localization" md "%SolutionDir%Publish\WindowsPrerelease\Localization"
)

if "%CurrentConfiguration%" == "Release" (
	echo R-LOC
	if not exist  "%SolutionDir%Publish\Windows\Localization" md "%SolutionDir%Publish\Windows\Localization"
)

echo making start scripts
@(echo @echo off && echo @title IW4MAdmin && echo dotnet Lib\IW4MAdmin.dll && echo pause) > "%SolutionDir%Publish\WindowsPrerelease\StartIW4MAdmin.cmd"
@(echo @echo off && echo @title IW4MAdmin && echo dotnet Lib\IW4MAdmin.dll && echo pause) > "%SolutionDir%Publish\Windows\StartIW4MAdmin.cmd"

@(echo #!/bin/bash && echo dotnet Lib/IW4MAdmin.dll) > "%SolutionDir%Publish\WindowsPrerelease\StartIW4MAdmin.sh"
@(echo #!/bin/bash && echo dotnet Lib/IW4MAdmin.dll) > "%SolutionDir%Publish\Windows\StartIW4MAdmin.sh"
