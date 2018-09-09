set SolutionDir=%1
set ProjectDir=%2
set TargetDir=%3

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

echo making start scripts
@echo dotnet IW4MAdmin.dll > "%SolutionDir%Publish\WindowsPrerelease\StartIW4MAdmin.cmd"
@echo dotnet IW4MAdmin.dll > "%SolutionDir%Publish\Windows\StartIW4MAdmin.cmd"

@(echo #!/bin/bash && echo dotnet IW4MAdmin.dll) > "%SolutionDir%Publish\WindowsPrerelease\StartIW4MAdmin.sh"
@(echo #!/bin/bash && echo dotnet IW4MAdmin.dll) > "%SolutionDir%Publish\Windows\StartIW4MAdmin.sh"
