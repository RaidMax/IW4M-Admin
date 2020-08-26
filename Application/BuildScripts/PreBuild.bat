set SolutionDir=%1
set ProjectDir=%2
set TargetDir=%3

echo D | xcopy "%SolutionDir%Plugins\ScriptPlugins\*.js" "%TargetDir%Plugins" /y
powershell -File "%ProjectDir%BuildScripts\DownloadTranslations.ps1" %TargetDir%