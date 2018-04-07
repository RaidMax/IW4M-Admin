set SolutionDir=%1
set ProjectDir=%2
set TargetDir=%3

xcopy /Y "%SolutionDir%BUILD\Plugins" "%SolutionDir%Publish\Plugins\"
xcopy /Y "%SolutionDir%SharedLibrary\LibSQLCe\x86" "%SolutionDir%Publish\x86\"