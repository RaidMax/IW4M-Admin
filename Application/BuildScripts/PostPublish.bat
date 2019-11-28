set PublishDir=%1
SET COPYCMD=/Y

echo deleting extra runtime files
if exist "%PublishDir%\runtimes\linux-arm" powershell Remove-Item -Force -Recurse '%PublishDir%\runtimes\linux-arm'
if exist "%PublishDir%\runtimes\linux-arm64" powershell Remove-Item -Force -Recurse '%PublishDir%\runtimes\linux-arm64'
if exist "%PublishDir%\runtimes\linux-armel" powershell Remove-Item -Force -Recurse '%PublishDir%\runtimes\linux-armel'
if exist "%PublishDir%\runtimes\osx" powershell Remove-Item -Force -Recurse '%PublishDir%\runtimes\osx'
if exist "%PublishDir%\runtimes\osx-x64" powershell Remove-Item -Force -Recurse '%PublishDir%\runtimes\osx-x64'
if exist "%PublishDir%\runtimes\win-arm" powershell Remove-Item -Force -Recurse '%PublishDir%\runtimes\win-arm'
if exist "%PublishDir%\runtimes\win-arm64" powershell Remove-Item -Force -Recurse '%PublishDir%\runtimes\win-arm64'
if exist "%PublishDir%\runtimes\alpine-x64" powershell Remove-Item -Force -Recurse '%PublishDir%\runtimes\alpine-x64'
if exist "%PublishDir%\runtimes\linux-musl-x64" powershell Remove-Item -Force -Recurse '%PublishDir%\runtimes\linux-musl-x64'

echo deleting misc files
if exist "%PublishDir%\web.config" del "%PublishDir%\web.config"
if exist "%PublishDir%\libman.json" del "%PublishDir%\libman.json"
del "%PublishDir%\*.exe"
del "%PublishDir%\*.pdb"

echo setting up default folders
if not exist "%PublishDir%\Configuration" md "%PublishDir%\Configuration"
move "%PublishDir%\DefaultSettings.json" "%PublishDir%\Configuration\"
if not exist "%PublishDir%\Lib\" md "%PublishDir%\Lib\"
move "%PublishDir%\*.dll" "%PublishDir%\Lib\"
move "%PublishDir%\*.json" "%PublishDir%\Lib\"
move "%PublishDir%\runtimes" "%PublishDir%\Lib\runtimes"
if exist "%PublishDir%\refs" move "%PublishDir%\refs" "%PublishDir%\Lib\refs"
if not exist "%PublishDir%\Localization" md "%PublishDir%\Localization"

echo making start scripts
@(echo @echo off && echo @title IW4MAdmin && echo set DOTNET_CLI_TELEMETRY_OPTOUT=1 && echo dotnet Lib\IW4MAdmin.dll && echo pause) > "%PublishDir%\StartIW4MAdmin.cmd"
@(echo #!/bin/bash&& echo export DOTNET_CLI_TELEMETRY_OPTOUT=1&& echo dotnet Lib/IW4MAdmin.dll) > "%PublishDir%\StartIW4MAdmin.sh"

echo moving front-end library dependencies
if not exist "%PublishDir%\wwwroot\font" mkdir "%PublishDir%\wwwroot\font"
move "%PublishDir%\wwwroot\lib\open-iconic\font\fonts\*.*" "%PublishDir%\wwwroot\font\"
rd /s /q "%PublishDir%\wwwroot\lib"

echo setting permissions...
cacls "%PublishDir%" /t /e /p Everyone:F