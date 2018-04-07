set SolutionDir=%1
set ProjectDir=%2
set TargetDir=%3

if "TargetDir" == "*Undefined*" (
	echo "Copying extra files to publish dir"
	xcopy /Y "%SolutionDir%BUILD\Plugins" "%SolutionDir%Publish\Plugins\"
	xcopy /Y "%SolutionDir%SharedLibrary\LibSQLCe\x86" "%SolutionDir%Publish\x86\"
)