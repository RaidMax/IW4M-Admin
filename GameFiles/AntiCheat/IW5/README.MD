# IW5

This expands IW4M-Admins's Anti-cheat to Plutonium IW5
## Installation

Add ``_customcallbacks.gsc`` into the scripts folder. (%localappdata%\Plutonium\storage\iw5\scripts)

For more info check out Chase's [how-to guide](https://forum.plutonium.pw/topic/10738/tutorial-loading-custom-gsc-scripts).

You need to add this to you ``StatsPluginSettings.json`` found in your IW4M-Admin configuration folder.

```
      "IW5": {
        "Recoil": [
          "iw5_1887_mp.*",
          "turret_minigun_mp"
        ],
        "Button": [
          ".*akimbo.*"
        ]
      }
```
[Example](https://imgur.com/Ji9AafI)