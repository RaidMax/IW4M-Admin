set SolutionDir=%1
set ProjectDir=%2
set TargetDir=%3

if not exist "%ProjectDir%LibSQLCE\x86" (
	md "%TargetDir%x86" xcopy /y "%ProjectDir%LibSQLCE\x86" "%TargetDir%x86\"
)