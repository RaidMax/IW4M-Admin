dotnet publish WebfrontCore/WebfrontCore.csproj -c Release -o C:\Projects\IW4M-Admin\Publish\Windows
dotnet publish Application/Application.csproj -c Release -o C:\Projects\IW4M-Admin\Publish\Windows
dotnet publish GameLogServer/GameLogServer.pyproj -c Release -o C:\Projects\IW4M-Admin\Publish\Windows\GameLogServer
call "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\Tools\VsDevCmd.bat"
msbuild  GameLogServer/GameLogServer.pyproj  /p:PublishProfile=Stable /p:DeployOnBuild=true /p:PublishProfileRootFolder=C:\Projects\IW4M-Admin\GameLogServer\
msbuild  DiscordWebhook/DiscordWebhook.pyproj  /p:PublishProfile=Stable /p:DeployOnBuild=true /p:PublishProfileRootFolder=C:\Projects\IW4M-Admin\DiscordWebhook\
cd "C:\Projects\IW4M-Admin\DEPLOY\"
PowerShell ".\upload_release.ps1"