dotnet publish WebfrontCore/WebfrontCore.csproj -c Prerelease -o C:\Projects\IW4M-Admin\Publish\WindowsPrerelease
dotnet publish Application/Application.csproj -c Prerelease -o C:\Projects\IW4M-Admin\Publish\WindowsPrerelease
dotnet publish GameLogServer/GameLogServer.pyproj -c Release -o C:\Projects\IW4M-Admin\Publish\WindowsPrerelease\GameLogServer
call "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\Tools\VsDevCmd.bat"
msbuild  GameLogServer/GameLogServer.pyproj  /p:PublishProfile=FolderProfile /p:DeployOnBuild=true /p:PublishProfileRootFolder=C:\Projects\IW4M-Admin\GameLogServer\
msbuild  DiscordWebhook/DiscordWebhook.pyproj  /p:PublishProfile=FolderProfile /p:DeployOnBuild=true /p:PublishProfileRootFolder=C:\Projects\IW4M-Admin\DiscordWebhook\
cd "C:\Projects\IW4M-Admin\DEPLOY\"
PowerShell ".\upload_prerelease.ps1"