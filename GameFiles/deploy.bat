@echo off

ECHO "Pluto IW5"
xcopy /y .\GameInterface\_integration_base.gsc "%LOCALAPPDATA%\Plutonium\storage\iw5\scripts"
xcopy /y .\GameInterface\_integration_shared.gsc "%LOCALAPPDATA%\Plutonium\storage\iw5\scripts"
xcopy /y .\GameInterface\_integration_iw5.gsc "%LOCALAPPDATA%\Plutonium\storage\iw5\scripts"
xcopy /y .\AntiCheat\IW5\storage\iw5\scripts\_customcallbacks.gsc "%LOCALAPPDATA%\Plutonium\storage\iw5\scripts\mp"

ECHO "Pluto T5"
xcopy /y .\GameInterface\_integration_base.gsc "%LOCALAPPDATA%\Plutonium\storage\t5\scripts"
xcopy /y .\GameInterface\_integration_shared.gsc "%LOCALAPPDATA%\Plutonium\storage\t5\scripts"
xcopy /y .\GameInterface\_integration_t5.gsc "%LOCALAPPDATA%\Plutonium\storage\t5\scripts\mp"
xcopy /y .\GameInterface\_integration_t5zm.gsc "%LOCALAPPDATA%\Plutonium\storage\t5\scripts\sp\zom"

ECHO "Pluto T6"
xcopy /y .\AntiCheat\PT6\storage\t6\scripts\mp\_customcallbacks.gsc "%LOCALAPPDATA%\Plutonium\storage\t6\scripts\mp"
xcopy /y .\AntiCheat\PT6\storage\t6\scripts\mp\_customcallbacks.gsc.src "%LOCALAPPDATA%\Plutonium\storage\t6\scripts\mp"
