# Game Interface

Allows integration of IW4M-Admin to GSC, mainly used for special commands that need to use GSC in order to work.
But can also be used to read / write metadata from / to a profile and to get the player permission level.

## Installation Guide


The documentation can be found here: [GameInterface](https://github.com/RaidMax/IW4M-Admin/wiki/GameInterface)

## Latency Probe

The Game Interface includes a built-in latency probe (`MonitorLatencyProbe` in `_integration_shared.gsc`) that allows IW4MAdmin to measure log pipeline latency. This is automatic when the Game Interface is installed — no additional configuration is needed.
