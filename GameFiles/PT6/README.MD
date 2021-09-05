# T6

This brings IW4M-Admins's Anti-cheat to Plutonium T6
The following limitations are known:
* Can't get the recoil from weapons fire; you have to disable this detection type manually.
* in extreme cases it can produce false positives for Snap and Offset detection.

## Installation


Move `_customcallbacks.gsc` to `%localappdata%\Plutonium\storage\t6\scripts\mp\`

Add this to the WeaponNameParserConfigurations List in the StatsPluginSettings.json file:

```
    {
      "Game": "T6",
      "Delimiters": [
        "_",
        "+"
      ],
      "WeaponSuffix": "mp",
      "WeaponPrefix": null
    }
```

Now create the following entry for __EVERY__ T6 server you are using this on in the ServerDetectionTypes list:

```
    "1270014976": [
        "Offset",
        "Strain",
        "Snap"
      ]
```