dotnet publish WebfrontCore/WebfrontCore.csproj -c Prerelease -f netcoreapp2.2 --force -o X:\IW4MAdmin\Publish\WindowsPrerelease /p:PublishProfile=Prerelease
dotnet publish Application/Application.csproj -c Prerelease -f netcoreapp2.2 --force -o X:\IW4MAdmin\Publish\WindowsPrerelease /p:PublishProfile=Prerelease
dotnet publish GameLogServer/GameLogServer.pyproj -c Release -f netcoreapp2.2 --force -o X:\IW4MAdmin\Publish\WindowsPrerelease\GameLogServer
call "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\Tools\VsDevCmd.bat"
msbuild  GameLogServer/GameLogServer.pyproj  /p:PublishProfile=PreRelease /p:DeployOnBuild=true /p:PublishProfileRootFolder=X:\IW4MAdmin\GameLogServer\
cd "X:\IW4MAdmin\DEPLOY\"
PowerShell ".\upload_prerelease.ps1"