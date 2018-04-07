set SolutionDir=%1
set ProjectDir=%2
set TargetDir=%3

if not exist "%SolutionDir%BUILD" (
	echo "Making build dir"
	mkdir  "%SolutionDir%BUILD"
)

if not exist "%SolutionDir%BUILD\Plugins" (
	echo "Making plugin dir"
	mkdir  "%SolutionDir%BUILD\Plugins"
) 

if not exist "%SolutionDir%BUILD\userraw\scripts" (
	echo "Making userraw dir"
	mkdir "%SolutionDir%BUILD\userraw\scripts"
)

copy "%SolutionDir%_customcallbacks.gsc" "%SolutionDir%BUILD\userraw\scripts\_customcallbacks.gsc"