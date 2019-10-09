dotnet publish WebfrontCore/WebfrontCore.csproj -c Release -f netcoreapp3.0 --force -o X:\IW4MAdmin\Publish\Windows /p:PublishProfile=sTABLE
dotnet publish Application/Application.csproj -c Release -f netcoreapp3.0 --force -o X:\IW4MAdmin\Publish\Windows /p:PublishProfile=Stable
dotnet publish GameLogServer/GameLogServer.pyproj -c Release -f netcoreapp3.0 --force -o X:\IW4MAdmin\Publish\WindowsPrerelease\GameLogServer
call "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\Common7\Tools\VsDevCmd.bat"
msbuild  GameLogServer/GameLogServer.pyproj  /p:PublishProfile=Stable /p:DeployOnBuild=true /p:PublishProfileRootFolder=X:\IW4MAdmin\GameLogServer\
cd "X:\IW4MAdmin\DEPLOY\"
PowerShell ".\upload_release.ps1"