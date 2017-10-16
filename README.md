# <span style="color: #007ACC;">IW4MAdmin</span>
### <span style="color: #007ACC; opacity:0.75;">Quick Start Guide</span>
### Version 1.4
_______

### Setup
IW4MAdmin requires minimal configuration to run. There is only one prerequisite.  
1. [.NET Framework 4.5](https://www.microsoft.com/en-us/download/details.aspx?id=30653) *or newer*  

Extract `IW4MAdmin.zip`  
Run `IW4MAdmin.exe`
___

### Configuration
_If you wish to customize your experience of IW4MAdmin, the following configuration files will allow you to changes core options._

`auth.cfg`
  * This is the configuration file that sets the authorization password for the [React](http://reactiongaming.us) client
  * Modify the _first_ line only

`maps.cfg`
  * This is the configuration file that links an IW4 map file name to it's common/ingame name
  * This can be safely modified to add additional SP/DLC maps

`messages.cfg`
  * This is the configuration file that broadcasts messages to your server at a set time
  * The _first line_ specifies the amount of time between messages (in seconds)
  * Every new line is interpreted as a new message
  * Color codes are allowed in the messages
  * Tokens are denoted by double braces: {{TOKEN}}

`rules.cfg`
  * This is the configuration file that sets the server's rules.
  * Every new line is interpreted as a new rule
  * All rules are _global_ across servers

`web.cfg`
  * This is the configuration file that specifies the web front bindings
  * The first line specifies the _IP or Hostname_ to bind to
  * The second line specifies the _port_ to bind to

___
### Commands
|Name          |Description                                             |Requires Target|Arguments      |Required Level|
|--------------|--------------------------------------------------------|---------------|---------------|--------------|
|owner         |claim ownership of your server                          | False         | 0             | None         |
|setlevel      |set player to specified administration level            | True          | 2             | Owner        |
|rcon          |send rcon command to server                             | False         | 1             | Owner        |
|ban           |permanently ban a player from the server                | True          | 2             | SeniorAdmin  |
|unban         |unban player by database id                             | True          | 1             | SeniorAdmin  |
|find          |find player in the database                             | False         | 1             | SeniorAdmin  |
|maprotate     |cycle to next map in rotation                           | False         | 0             | Administrator|
|map           |change to specified map                                 | False         | 1             | Administrator|
|mask          |hide your online presence from online admin list        | False         | 0             | Administrator|
|plugins       |view all loaded plugins                                 | False         | 0             | Administrator|
|say           |broadcast message to all players                        | False         | 1             | Moderator    |
|tempban       |temporarily ban a player for 1 hour                     | True          | 2             | Moderator    |
|list          |list all active clients                                 | False         | 0             | Moderator    |
|fastrestart   |fast restart current map                                | False         | 0             | Moderator    |
|usage         |get current application memory usage                    | False         | 0             | Moderator    |
|uptime        |get current application running time                    | False         | 0             | Moderator    |
|balance       |balance the in game teams                               | False         | 0             | Moderator    |
|flag          |flag a suspicious player and announce to admins on join | True          | 1             | Moderator    |
|reports       |get most recent reports                                 | False         | 1 _opt_       | Moderator    |
|tell          |send onscreen message to player                         | True          | 2             | Moderator    |
|baninfo       |get information about a ban for a player                | True          | 1             | Moderator    |
|alias         |get past aliases and ips of a player                    | True          | 1             | Moderator    |
|findall       |find a player by their aliase(s)                        | False         | 1             | Moderator    |
|whoami        |list information about yourself                         | False         | 0             | User         |
|help          |list all available commands                             | False         | 0             | User         |
|admins        |list currently connected admins                         | False         | 0             | User         |
|rules         |list server rules                                       | False         | 0             | User         |
|privatemessage|send message to other player                            | True          | 2             | User         |
|report        |report a player for suspicious behavior                 | True          | 2             | User         |

#### Player Identification
All players are identified 4 ways   
1. `npID/GUID/XUID` - The ID corresponding to the player's hardware or forum account   
2. `IP` - The player's IP Address   
3. `Database ID` - The internal reference to a player, generated by IW4MAdmin   
4. `Name` - The visible player name as it appears in game   

For most commands players are identified by their `Name`  
However, if they are currently offline, or their name contains un-typable characters, a `Database ID` must be used   

The `dbID` is specified by prefixing a player's reference number with `@`.  
For example, `@123` would reference the player with a `dbID` of 123.  
Players can also be referenced by `clientID`, which is simply their slot (0 - 17)

**All commands that require a `target` look at the `first argument` for a form of player identification**


#### Additional Command Parameters
`setlevel`
- _shortcut_ - `sl`
- _Parameter 1_ - Player to modify level of
- _Parameter 2_ - Level to set the player to ```[ User, Trusted, Moderator, Administrator, SeniorAdmin ]```
- _Example_ - `!setlevel Player1 SeniorAdmin`, `!sl @123 Moderator`
- **NOTE** - It has been purposefully designed that there should only be **1 Owner** ( owner cannot set another player's level to owner unless the configuration option is enabled during setup)

`ban`
- _Shortcut_ - `b`
- _Parameter 1_ - Player to ban
- _Parameter 2_ - Reason for ban
- _Example_ - `!ban Player1 caught cheating`, `!b @123 GUID Spoofing`

`reports`  
- _Shortcut_ - `reps`
- _Optional Parameter 1_ - `clear` (erases reports for current server)

___
### Plugins
#### EventAPI
- This plugin adds a page to the webfront that serves JSON content in the form of server events   
- The page is located at 127.0.0.1/api/events
- JSON Object Structure
 	* **eventCount** - Number of events in the generated response ( 0 or 1 )
 	* **Event** - The event object corresponding to generated event ( will be null if eventCount = 0 )
 	  * _Version_ - The supported version of the Event Object ( IW4MAdmin = 0 )
 	  * _Type_ - The type of Event Object ( Notification = 0, Status = 1, Alert = 2 )
 	  * _Message_ - The string contents of the Event Object ( ie chat message text )
 	  * _Title_ - The string header/title of the Event Object ( optional )
 	  * _Origin_ - The string origin of the Event Object ( ie player name or sv_hostname )
 	  * _Target_ - The string target of the Event Object ( ie reported player's name )
 	  * _ID_ - The int ID of the Event Object ( will be unique unless two events are generated simultaneously )
- Optional Parameters 
  * appending a `GET` parameter of `status=1` to your request will generate a list of currently monitored servers
  * For example: 127.0.0.1/api/events?status=1
  * The contents of the response will be in the `Message` property of the response
- Each event will be consumed ( eaten by the request, so save the event if you need to use it later )
- The plugin additionally scans chat messages for phrases that indicate a cheater on the server
- If enough matching phrases are detected, an alert will be generated
- No commands are added by this plugin
- Additional Features will be added in the future   
#### Welcome   
- This plugin uses geolocation data to welcome a player based on their IP's country
- All privileged users ( Trusted or higher ) recieve a specialized welcome message as well
- For React servers, the password authentication system is bundled in this plugin..  

**Commands added by this plugin**    


|Name          |Description                                             |Requires Target|Arg Count      |Required Level|
|--------------|--------------------------------------------------------|---------------|---------------|--------------|
|login         |authenticate with IW4MAdmin.                            | False         | 1             | User         |

#### Stats
- This plugin calculates basic player performance, skill approximation, and kill/death ratio
- Total play-time is stored by this plugin
- After 3 days ( 36 hours ) of total play-time, a user earns the `Trusted` rank ( will be optional in a later release )  

**Commands added by this plugin** 


|Name          |Description                                             |Requires Target|Arg Count      |Required Level|
|--------------|--------------------------------------------------------|---------------|---------------|--------------|
|stats         |view your or another player's stats                     | False         | 1 _opt_       | User         |
|topstats      |view the top 5 players on this server                   | False         | 0             | User         |

- To qualify for top stats, a player must meet the following criteria
  * `Skill` > 10
  * `Kills` > 150
  * `Play Time` > 1 hour

- Each server has seperated stats and can be reset by deleting `stats_<port>.rm`

___
### Misc
#### Database Storage
All unique client information is stored in `clients.rm`. Should you need to reset your database, this file can simply be deleted.
Player aliases and previous ips are stored in `aliases.rm`.
