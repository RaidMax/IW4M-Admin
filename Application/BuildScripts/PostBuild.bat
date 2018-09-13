set SolutionDir=%1
set ProjectDir=%2
set TargetDir=%3
set OutDir=%4
set Version=%5

echo %Version% > "%SolutionDir%DEPLOY\version.txt"

echo Copying dependency configs
copy "%SolutionDir%WebfrontCore\%OutDir%*.deps.json" "%TargetDir%"
copy "%SolutionDir%SharedLibaryCore\%OutDir%*.deps.json" "%TargetDir%"

if not exist "%TargetDir%Plugins" (
	echo "Making plugin dir"
	md "%TargetDir%Plugins"
)

xcopy /y "%SolutionDir%Build\Plugins" "%TargetDir%Plugins\"

echo Copying plugins for publish
del %SolutionDir%BUILD\Plugins\Tests.dll
xcopy /Y "%SolutionDir%BUILD\Plugins" "%SolutionDir%Publish\Windows\Plugins\"
xcopy /Y "%SolutionDir%BUILD\Plugins" "%SolutionDir%Publish\WindowsPrerelease\Plugins\"

echo Copying script plugins for publish
xcopy /Y "%SolutionDir%Plugins\ScriptPlugins" "%SolutionDir%Publish\Windows\Plugins\"
xcopy /Y "%SolutionDir%Plugins\ScriptPlugins" "%SolutionDir%Publish\WindowsPrerelease\Plugins\"

echo Copying GSC files for publish
xcopy /Y  "%SolutionDir%_customcallbacks.gsc" "%SolutionDir%Publish\Windows\userraw\scripts\"
xcopy /Y "%SolutionDir%_customcallbacks.gsc" "%SolutionDir%Publish\WindowsPrerelease\userraw\scripts\"

xcopy /Y "%SolutionDir%_commands.gsc" "%SolutionDir%Publish\Windows\userraw\scripts\"
xcopy /Y "%SolutionDir%_commands.gsc" "%SolutionDir%Publish\WindowsPrerelease\userraw\scripts\"