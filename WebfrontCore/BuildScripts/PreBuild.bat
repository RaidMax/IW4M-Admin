set SolutionDir=%1
set ProjectDir=%2

if not exist "%SolutionDir%BUILD" (
	mkdir  "%SolutionDir%BUILD"
) 

if not exist "%SolutionDir%BUILD\userraw\scripts" (
	mkdir "%SolutionDir%BUILD\userraw\scripts"
)