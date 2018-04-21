

# IW4MAdmin
### Quick Start Guide
### Version 2.0
_______
### About
**IW4MAdmin** is an administration tool for [IW4x](https://iw4xcachep26muba.onion.link/), [T6M](https://plutonium.pw/), and most Call of Duty® dedicated servers. It allows complete control of your server; from changing maps, to banning players, **IW4MAdmin** monitors and records activity on your server(s). With plugin support, extending its functionality is a breeze.

### Setup
**IW4MAdmin** requires minimal configuration to run. There is only one prerequisite.  
* [.NET Core 2.0 Runtime](https://www.microsoft.com/net/download/dotnet-core/runtime-2.0.5) *or newer*  

1. Extract `IW4MAdmin-<version>.zip`  
2. Open command prompt or terminal in the extracted folder
3. Run `>dotnet IW4MAdmin.dll`
___

### Configuration
#### Initial Configuration
When **IW4MAdmin** is launched for the _first time_, you will be prompted to setup your configuration.

`Enable webfront`
* Enables you to monitor and control your server(s) through a web interface [defaults to `http://127.0.0.1:1624`]

`Enable multiple owners`
* Enables more than one client to be promoted to level of `Owner`

`Enable stepped privilege hierarchy`
* Allows privileged clients to promote other clients to the level below their current level

`Enable custom say name`
* Shows a prefix to every message send by **IW4MAdmin** -- `[Admin] message`
* _This feature requires you specify a custom say name_

`Enable client VPNs`
* Allow clients to use a [VPN](https://en.wikipedia.org/wiki/Virtual_private_network) 
* _This feature requires an active api key on [iphub.info](https://iphub.info/)_

`Enable discord link`
* Shows a link to your server's discord on the webfront
* _This feature requires an invite link to your discord server_

`Use Custom Encoding Parser`
* Allows alternative encodings to be used for parsing game information and events
* **Russian users should use this and then specify** `windows-1251` **as the encoding string**

#### Advanced Configuration
If you wish to further customize your experience of **IW4MAdmin**, the following configuration file(s) will allow you to changes core options using any text-editor.

#### `IW4MAdminSettings.json`-- _this file is created after initial setup_
* This file uses the [JSON](https://en.wikipedia.org/wiki/JSON#JSON_sample) specification, so please validate it before running **IW4MAdmin**

`WebfrontBindUrl`
* Specifies the address and port the webfront will listen on.
* The value can be an [IP Address](https://en.wikipedia.org/wiki/IP_address):port or [Domain Name](https://en.wikipedia.org/wiki/Domain_name):port

`Servers`
* Specifies the list of servers **IW4MAdmin** will monitor
* `IPAddress`
	* Specifies the IP Address of the particular server
* `Port`
	* Specifies the port of the particular server
* `Password`
	* Specifies the `rcon_password` of the particular server
* `AutoMessages`
	* Specifies the list of messages that are broadcasted to the particular server
* `Rules`
	* Specifies the list of rules that apply to the particular server

`AutoMessagePeriod`
* Specifies (in seconds) how often messages should be broadcasted to the server(s)

`AutoMessages`
* Specifies the list of messages that are broadcasted to **all** servers

`GlobalRules`
* Specifies the list of rules that apply to **all** servers`

`Maps`
* Specifies the list of maps for each supported game
* `Name`
	* Specifies the name of the map as returned by the game
* `Alias`
	* Specifies the display name of the map (as seen while loading in)
___

### Commands
|Name              |Alias|Description                                                                               |Requires Target|Syntax           |Required Level|
|--------------| -----| --------------------------------------------------------| -----------------| -------------| ----------------|
|prune|pa|demote any admins that have not connected recently (defaults to 30 days)|False|!pa \<optional inactive days\>|Owner|
|quit|q|quit IW4MAdmin|False|!q |Owner|
|rcon|rcon|send rcon command to server|False|!rcon \<command\>|Owner|
|ban|b|permanently ban a player from the server|True|!b \<player\> \<reason\>|SeniorAdmin|
|unban|ub|unban player by database id|True|!ub \<databaseID\> \<reason\>|SeniorAdmin|
|find|f|find player in database|False|!f \<player\>|Administrator|
|killserver|kill|kill the game server|False|!kill |Administrator|
|map|m|change to specified map|False|!m \<map\>|Administrator|
|maprotate|mr|cycle to the next map in rotation|False|!mr |Administrator|
|plugins|p|view all loaded plugins|False|!p |Administrator|
|alias|known|get past aliases and ips of a player|True|!known \<player\>|Moderator|
|baninfo|bi|get information about a ban for a player|True|!bi \<player\>|Moderator|
|fastrestart|fr|fast restart current map|False|!fr |Moderator|
|flag|fp|flag a suspicious player and announce to admins on join|True|!fp \<player\> \<reason\>|Moderator|
|list|l|list active clients|False|!l |Moderator|
|mask|hide|hide your presence as an administrator|False|!hide |Moderator|
|reports|reps|get or clear recent reports|False|!reps \<optional clear\>|Moderator|
|say|s|broadcast message to all players|False|!s \<message\>|Moderator|
|setlevel|sl|set player to specified administration level|True|!sl \<player\> \<level\>|Moderator|
|setpassword|sp|set your authentication password|False|!sp \<password\>|Moderator|
|tempban|tb|temporarily ban a player for specified time (defaults to 1 hour)|True|!tb \<player\> \<duration (m\|h\|d\|w\|y)\> \<reason\>|Moderator|
|uptime|up|get current application running time|False|!up |Moderator|
|usage|us|get current application memory usage|False|!us |Moderator|
|kick|k|kick a player by name|True|!k \<player\> \<reason\>|Trusted|
|login|l|login using password|False|!l \<password\>|Trusted|
|warn|w|warn player for infringing rules|True|!w \<player\> \<reason\>|Trusted|
|warnclear|wc|remove all warning for a player|True|!wc \<player\>|Trusted|
|admins|a|list currently connected admins|False|!a |User|
|getexternalip|ip|view your external IP address|False|!ip |User|
|help|h|list all available commands|False|!h \<optional command\>|User|
|ping|pi|get client's ping|False|!pi \<optional client\>|User|
|privatemessage|pm|send message to other player|True|!pm \<player\> \<message\>|User|
|report|rep|report a player for suspicious behavior|True|!rep \<player\> \<reason\>|User|
|resetstats|rs|reset your stats to factory-new|False|!rs |User|
|rules|r|list server rules|False|!r |User|
|stats|xlrstats|view your stats|False|!xlrstats \<optional player\>|User|
|topstats|ts|view the top 5 players on this server|False|!ts |User|
|whoami|who|give information about yourself.|False|!who |User|

_These commands include all shipped plugin commands._

---

#### Player Identification
All players are identified 4 seperate ways   
1. `npID/GUID/XUID` - The ID corresponding to the player's hardware or forum account   
2. `IP` - The player's IP Address   
3. `Client ID` - The internal reference to a player, generated by **IW4MAdmin**   
4. `Name` - The visible player name as it appears in game   

For most commands players are identified by their `Name`  
However, if they are currently offline, or their name contains un-typable characters, their `Client ID` must be used   

The `Client ID` is specified by prefixing a player's reference number with `@`.  
For example, `@123` would reference the player with a `Client ID` of 123.  
While in-game, [layers can also be referenced by `Client Number`, which is simply their slot [0 - 17]

**All commands that require a `target` look at the `first argument` for a form of player identification**

---

#### Additional Command Examples
`setlevel`
- _shortcut_ - `sl`
- _Parameter 1_ - Player to modify level of
- _Parameter 2_ - Level to set the player to ```[ User, Trusted, Moderator, Administrator, SeniorAdmin, Owner ]```
- _Example_ - `!setlevel Player1 SeniorAdmin`, `!sl @123 Moderator`
- **NOTE** - An `owner` cannot set another player's level to `owner` unless the configuration option is enabled during setup

`ban`
- _Shortcut_ - `b`
- _Parameter 1_ - Player to ban
- _Parameter 2_ - Reason for ban
- _Example_ - `!ban Player1 caught cheating`, `!b @123 GUID Spoofing`

`tempban`
- _Shortcut_ - `tb`
- _Parameter 1_ - Player to ban
- _Parameter 2_ - Ban length (minutes|hours|days|weeks|years)
- _Parameter 3_ - Reason for ban
- _Example_ - `!tempban Player1 3w racism`, `!tb @123 8h Abusive behaivor`

`reports`  
- _Shortcut_ - `reps`
- _Optional Parameter 1_ - `clear` (erases reports for current server)

___
### Plugins

#### Welcome   
- This plugin uses geo-location data to welcome a player based on their country of origin
- All privileged users ( Trusted or higher ) receive a specialized welcome message as well 
- Welcome messages can be customized in `WelcomePluginSettings.json`

#### Stats
- This plugin calculates basic player performance, skill approximation, and kill/death ratio 

**Commands added by this plugin** 

|Name              |Alias|Description                                                                               |Requires Target|Syntax           |Required Level|
|--------------| -----| --------------------------------------------------------| -----------------| -------------| ----------------|
|resetstats|rs|reset your stats to factory-new|False|!rs |User|
|stats|xlrstats|view your stats|False|!xlrstats \<optional player\>|User|
|topstats|ts|view the top 5 players on this server|False|!ts |User|

- To qualify for top stats, a client must have played for at least `1 hour` and connected within the past `30 days`.

#### Login
- This plugin deters GUID spoofing by requiring privileged users to login with their password before executing commands
- A password must be set using the `setpassword` command before logging in

 **Commands added by this plugin**  

|Name              |Alias|Description                                                                               |Requires Target|Syntax           |Required Level|
|--------------| -----| --------------------------------------------------------| -----------------| -------------| ----------------|
|login|l|login using password|False|!l \<password\>|Trusted|

#### Profanity Determent
- This plugin warns and kicks players for using profanity
- Profane words and warning message can be specified in `ProfanityDetermentSettings.json`
___
### Webfront
`Home`
* Shows an overview of the monitored server(s)

`Penalties`
* Shows a chronological ordered list of client penalties (scrolling down loads older penalties)

`Admins`
* Shows a list of privileged clients

`Login`
* Allows privileged users to login using their `Client ID` and password set via `setpassword`

`Profile`
* Shows a client's information and history 

---

### Misc
#### Database Storage
All **IW4MAdmin** information is stored in `Database.db`. Should you need to reset your database, this file can simply be deleted. Additionally, this file should be preserved during updates to retain client information.