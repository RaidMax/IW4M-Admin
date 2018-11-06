dotnet publish WebfrontCore/WebfrontCore.csproj -c Prerelease -o X:\IW4MAdmin\Publish\WindowsPrerelease
dotnet publish Application/Application.csproj -c Prerelease -o X:\IW4MAdmin\Publish\WindowsPrerelease
dotnet publish GameLogServer/GameLogServer.pyproj -c Release -o X:\IW4MAdmin\Publish\WindowsPrerelease\GameLogServer
call "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\Tools\VsDevCmd.bat"
msbuild  GameLogServer/GameLogServer.pyproj  /p:PublishProfile=FolderProfile /p:DeployOnBuild=true /p:PublishProfileRootFolder=X:\IW4MAdmin\GameLogServer\
msbuild  DiscordWebhook/DiscordWebhook.pyproj  /p:PublishProfile=FolderProfile /p:DeployOnBuild=true /p:PublishProfileRootFolder=X:\IW4MAdmin\DiscordWebhook\
cd "X:\IW4MAdmin\DEPLOY\"
PowerShell ".\upload_prerelease.ps1"