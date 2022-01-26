set SolutionDir=%1
set ProjectDir=%2
set TargetDir=%3
set OutDir=%4
set Version=%5

echo Copying dependency configs
copy "%SolutionDir%WebfrontCore\%OutDir%*.deps.json" "%TargetDir%"
copy "%SolutionDir%SharedLibraryCore\%OutDir%*.deps.json" "%TargetDir%"

if not exist "%TargetDir%Plugins" (
	echo "Making plugin dir"
	md "%TargetDir%Plugins"
)

xcopy /y "%SolutionDir%Build\Plugins" "%TargetDir%Plugins\"
del "%TargetDir%Plugins\SQLite*"
