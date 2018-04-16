set SolutionDir=%1
set ProjectDir=%2
set TargetDir=%3

echo Deleting extra language files
if exist  "%SolutionDir%Publish\Windows\de\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\de'
if exist "%SolutionDir%Publish\Windows\es\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\es'
if exist "%SolutionDir%Publish\Windows\fr\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\fr'
if exist "%SolutionDir%Publish\Windows\it\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\it'
if exist "%SolutionDir%Publish\Windows\ja\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\ja'
if exist "%SolutionDir%Publish\Windows\ko\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\ko'
if exist "%SolutionDir%Publish\Windows\ru\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\ru'
if exist "%SolutionDir%Publish\Windows\zh-Hans\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\zh-Hans'
if exist "%SolutionDir%Publish\Windows\zh-Hant\" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\zh-Hant'

echo Deleting extra runtime files
if exist "%SolutionDir%Publish\Windows\runtimes\linux-arm" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\linux-arm'
if exist "%SolutionDir%Publish\Windows\runtimes\linux-arm64" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\linux-arm64'
if exist "%SolutionDir%Publish\Windows\runtimes\linux-armel" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\linux-armel'

if exist "%SolutionDir%Publish\Windows\runtimes\osx" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\osx'
if exist "%SolutionDir%Publish\Windows\runtimes\osx-x64" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\osx-x64'

if exist "%SolutionDir%Publish\Windows\runtimes\win-arm" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\win-arm'
if exist "%SolutionDir%Publish\Windows\runtimes\win-arm64" powershell Remove-Item -Force -Recurse '%SolutionDir%Publish\Windows\runtimes\win-arm64'

echo Deleting misc files
if exist "%SolutionDir%Publish\Windows\web.config" del "%SolutionDir%Publish\Windows\web.config"
del "%SolutionDir%Publish\Windows\*pdb"
