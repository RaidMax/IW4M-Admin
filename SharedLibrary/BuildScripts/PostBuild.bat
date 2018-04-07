set SolutionDir=%1
set ProjectDir=%2
set TargetDir=%3

if not exist "%TargetDir%x86" (
	echo "Copying SQLCe binaries"
	md "%TargetDir%x86"
	xcopy /y "%SolutionDir%SharedLibrary\LibSQLCe\x86" "%TargetDir%x86\"
)