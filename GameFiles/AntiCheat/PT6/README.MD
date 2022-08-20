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

Now update the `GameDetectionTypes` list with the following, if it does not already exist:

```
    "T6": [
        "Offset",
        "Snap",
        "Strain"
    ]
```
