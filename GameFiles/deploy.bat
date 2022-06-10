@echo off

ECHO "Pluto IW5"
xcopy /y .\_integration.gsc "%LOCALAPPDATA%\Plutonium\storage\iw5\scripts"
xcopy /y .\_integration_iw4_iw5.gsc "%LOCALAPPDATA%\Plutonium\storage\iw5\scripts"
xcopy /y .\IW5\storage\iw5\scripts\_customcallbacks.gsc "%LOCALAPPDATA%\Plutonium\storage\iw5\scripts"

ECHO "Pluto T5"
xcopy /y .\_integration.gsc "%LOCALAPPDATA%\Plutonium\storage\t5\scripts\mp"
xcopy /y .\_integration_t5.gsc "%LOCALAPPDATA%\Plutonium\storage\t5\scripts\mp"

ECHO "Pluto T6"
xcopy /y .\PT6\storage\t6\scripts\mp\_customcallbacks.gsc "%LOCALAPPDATA%\Plutonium\storage\t6\scripts\mp"
xcopy /y .\PT6\storage\t6\scripts\mp\_customcallbacks.gsc.src "%LOCALAPPDATA%\Plutonium\storage\t6\scripts\mp"