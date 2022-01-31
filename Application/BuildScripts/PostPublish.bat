set PublishDir=%1
set SourceDir=%2
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
del "%PublishDir%\Microsoft.CodeAnalysis*.dll" /F /Q
move "%PublishDir%\*.dll" "%PublishDir%\Lib\"
move "%PublishDir%\*.json" "%PublishDir%\Lib\"
move "%PublishDir%\runtimes" "%PublishDir%\Lib\runtimes"
move "%PublishDir%\ru" "%PublishDir%\Lib\ru"
move "%PublishDir%\de" "%PublishDir%\Lib\de"
move "%PublishDir%\pt" "%PublishDir%\Lib\pt"
move "%PublishDir%\es" "%PublishDir%\Lib\es"
rmdir /Q /S "%PublishDir%\cs"
rmdir /Q /S "%PublishDir%\fr"
rmdir /Q /S "%PublishDir%\it"
rmdir /Q /S "%PublishDir%\ja"
rmdir /Q /S "%PublishDir%\ko"
rmdir /Q /S "%PublishDir%\pl"
rmdir /Q /S "%PublishDir%\pt-BR"
rmdir /Q /S "%PublishDir%\tr"
rmdir /Q /S "%PublishDir%\zh-Hans"
rmdir /Q /S "%PublishDir%\zh-Hant"
if exist "%PublishDir%\refs" move "%PublishDir%\refs" "%PublishDir%\Lib\refs"

echo making start scripts
@(echo @echo off && echo @title IW4MAdmin && echo set DOTNET_CLI_TELEMETRY_OPTOUT=1 && echo dotnet Lib\IW4MAdmin.dll && echo pause) > "%PublishDir%\StartIW4MAdmin.cmd"
@(echo #!/bin/bash&& echo export DOTNET_CLI_TELEMETRY_OPTOUT=1&& echo dotnet Lib/IW4MAdmin.dll) > "%PublishDir%\StartIW4MAdmin.sh"

echo copying update scripts
copy "%SourceDir%\DeploymentFiles\UpdateIW4MAdmin.ps1" "%PublishDir%\UpdateIW4MAdmin.ps1"
copy "%SourceDir%\DeploymentFiles\UpdateIW4MAdmin.sh" "%PublishDir%\UpdateIW4MAdmin.sh"

echo moving front-end library dependencies
if not exist "%PublishDir%\wwwroot\font" mkdir "%PublishDir%\wwwroot\font"
move "WebfrontCore\wwwroot\lib\open-iconic\font\fonts\*.*" "%PublishDir%\wwwroot\font\"
if exist "%PublishDir%\wwwroot\lib" rd /s /q "%PublishDir%\wwwroot\lib"
if not exist "%PublishDir%\wwwroot\css" mkdir "%PublishDir%\wwwroot\css"
move "WebfrontCore\wwwroot\css\global.min.css" "%PublishDir%\wwwroot\css\global.min.css"
if not exist "%PublishDir%\wwwroot\js" mkdir "%PublishDir%\wwwroot\js"
move "%SourceDir%\WebfrontCore\wwwroot\js\global.min.js" "%PublishDir%\wwwroot\js\global.min.js"
if not exist "%PublishDir%\wwwroot\images" mkdir "%PublishDir%\wwwroot\images"
xcopy "%SourceDir%\WebfrontCore\wwwroot\images" "%PublishDir%\wwwroot\images" /E /H /C /I


echo setting permissions...
cacls "%PublishDir%" /t /e /p Everyone:F
