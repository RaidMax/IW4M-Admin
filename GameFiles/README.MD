# Game Interface

Allows integration of IW4M-Admin to GSC, mainly used for special commands that need to use GSC in order to work.
But can also be used to read / write metadata from / to a profile and to get the player permission level.

## Automatic installation


You can use `deploy.bat` on a Windows machine to automatically copy all necessary files to the Plutonium folder.
For IW4x, continue reading for a detailed manual installation guide.


## Installation Plutonium IW5


Move the following files to `%localappdata%\Plutonium\storage\iw5\scripts\`

- `_integration_base.gsc`
- `_integration_shared.gsc`
- `_integration_iw5.gsc`

If you are running a Multiplayer server you should also add this file to `%localappdata%\Plutonium\storage\iw5\scripts\mp`

- `_customcallbacks.gsc` which is found in this folder (shipped with IW4M): `AntiCheat\PT6\storage\iw5\scripts`


## Installation Plutonium T5


Move the following files to `%localappdata%\Plutonium\storage\t5\scripts\`

- `_integration_base.gsc`
- `_integration_shared.gsc`

If you are running a Multiplayer server you should also add this file to `%localappdata%\Plutonium\storage\t5\scripts\mp`

- `_integration_t5.gsc`

If you are running a Zombie server you should also add this file to `%localappdata%\Plutonium\storage\t5\scripts\sp\zom`

- `_integration_t5zm.gsc`


## Installation Plutonium T6


Move the following files to `%localappdata%\Plutonium\storage\t6\scripts\`

- `_integration_base.gsc`
- `_integration_shared.gsc`
- `_integration_t6.gsc`

If you are running a Multiplayer server you should also add this file to `%localappdata%\Plutonium\storage\t6\scripts\mp`

- `_customcallbacks.gsc` which is found in this folder (shipped with IW4M): `AntiCheat\PT6\storage\t6\scripts\mp`

If you are running a Zombie server you should also add this file to `%localappdata%\Plutonium\storage\t6\scripts\zm`

- `_integration_t6zm_help.gsc`

If you are using the plugin [t6-gsc-utils](https://github.com/fedddddd/t6-gsc-utils) you can also add this file to `%localappdata%\Plutonium\storage\t6\scripts\`

- `_integration_t6_file_bus.gsc`


## Installation IW4x


Move the following files to `IW4x/userraw/scripts`, `IW4x` being the root folder of your game server.

- `_integration_base.gsc`
- `_integration_shared.gsc`
- `_integration_iw4x.gsc`


You should also add this file to `IW4x/userraw/scripts`

- `_customcallbacks.gsc` which is found in this folder (shipped with IW4M): `AntiCheat\IW4x\userraw\scripts`
