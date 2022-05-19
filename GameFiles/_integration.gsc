#include common_scripts\utility;
#include maps\mp\_utility;
#include maps\mp\gametypes\_hud_util;

init()
{
    // setup default vars
    level.eventBus              = spawnstruct();
    level.eventBus.inVar        = "sv_iw4madmin_in";
    level.eventBus.outVar       = "sv_iw4madmin_out";
    level.eventBus.failKey      = "fail";
    level.eventBus.timeoutKey   = "timeout";
    level.eventBus.timeout      = 30;
    level.eventBus.gamename     = getDvar( "gamename" ); // We want to do a few small detail different on IW5 compared to IW4, nothing where 2 files would make sense.
    
    level.clientDataKey = "clientData";

    level.eventTypes                            = spawnstruct();
    level.eventTypes.localClientEvent           = "client_event";
    level.eventTypes.clientDataReceived         = "ClientDataReceived";
    level.eventTypes.clientDataRequested        = "ClientDataRequested";
    level.eventTypes.setClientDataRequested     = "SetClientDataRequested";
    level.eventTypes.setClientDataCompleted     = "SetClientDataCompleted";
    level.eventTypes.executeCommandRequested    = "ExecuteCommandRequested";
    
    level.iw4adminIntegrationDebug = false;
                                                                             
    SetDvarIfUninitialized( level.eventBus.inVar, "" );
    SetDvarIfUninitialized( level.eventBus.outVar, "" );
    SetDvarIfUninitialized( "sv_iw4madmin_integration_enabled", 1 );
    SetDvarIfUninitialized( "sv_iw4madmin_integration_debug", 0 );
    
    // map the event type to the handler
    level.eventCallbacks = [];
    level.eventCallbacks[level.eventTypes.clientDataReceived]       = ::OnClientDataReceived;
    level.eventCallbacks[level.eventTypes.executeCommandRequested]  = ::OnExecuteCommand; 
    level.eventCallbacks[level.eventTypes.setClientDataCompleted]   = ::OnSetClientDataCompleted;

    level.clientCommandCallbacks = [];
    level.clientCommandRusAsTarget = [];
    
    if ( GetDvarInt( "sv_iw4madmin_integration_enabled" ) != 1 )
    {
        return;
    }
    
    InitializeGameMethods();
    RegisterClientCommands();
    
    // start long running tasks
    level thread MonitorClientEvents();
    level thread MonitorBus();
    level thread OnPlayerConnect();
}



//////////////////////////////////
// Client Methods
//////////////////////////////////

OnPlayerConnect()
{
    level endon ( "disconnect" );
    
    for ( ;; )
    {
        level waittill( "connected", player );
        
        level.iw4adminIntegrationDebug = GetDvarInt( "sv_iw4madmin_integration_debug" );
        
        if ( isDefined(player.pers["isBot"]) && player.pers["isBot"] ) 
        {
            // we don't want to track bots
            continue;    
        }
        
        if ( !isDefined( player.pers[level.clientDataKey] ) )
        {
            player.pers[level.clientDataKey] = spawnstruct();
        }
        
        player thread OnPlayerSpawned();
        player thread OnPlayerJoinedTeam();
        player thread OnPlayerJoinedSpectators();
        player thread PlayerTrackingOnInterval();
        
        // only toggle if it's enabled
        if ( IsDefined( level.nightModeEnabled ) && level.nightModeEnabled )
        {
            player ToggleNightMode();
        }
    }
}

OnPlayerSpawned()
{
    self endon( "disconnect" );

    for ( ;; )
    {
        self waittill( "spawned_player" );
        self PlayerConnectEvents();
    }
}

OnPlayerDisconnect()
{
    level endon ( "disconnect" );

    for ( ;; )
    {
        self waittill( "disconnect" );
        self SaveTrackingMetrics();
    }
}

OnPlayerJoinedTeam()
{
    self endon( "disconnect" );

    for( ;; )
    {
        self waittill( "joined_team" );
        // join spec and join team occur at the same moment - out of order logging would be problematic
        wait( 0.25 ); 
        LogPrint( GenerateJoinTeamString( false ) );
    }
}

OnPlayerJoinedSpectators()
{
    self endon( "disconnect" );

    for( ;; )
    {
        self waittill( "joined_spectators" );
        LogPrint( GenerateJoinTeamString( true ) );
    }
}

OnGameEnded() 
{
    level endon ( "disconnect" );
    
    for ( ;; )
    {
        level waittill( "game_ended" );
        // note: you can run data code here but it's possible for 
        // data to get truncated, so we will try a timer based approach for now
    }
}

DisplayWelcomeData()
{
    self endon( "disconnect" );

    clientData = self.pers[level.clientDataKey];
    
    if ( clientData.permissionLevel == "User" || clientData.permissionLevel == "Flagged" ) 
    {
        return;
    } 
    
    self IPrintLnBold( "Welcome, your level is ^5" + clientData.permissionLevel );
    wait( 2.0 );
    self IPrintLnBold( "You were last seen ^5" + clientData.lastConnection );
}

PlayerConnectEvents() 
{
    self endon( "disconnect" );

    if ( IsDefined( self.isHidden ) && self.isHidden )
    {
        self HideImpl();
    }
    
    clientData = self.pers[level.clientDataKey];
    
    // this gives IW4MAdmin some time to register the player before making the request;
    // although probably not necessary some users might have a slow database or poll rate
    wait ( 2 );

    if ( isDefined( clientData.state ) && clientData.state == "complete" ) 
    {
        return;
    }
    
    self RequestClientBasicData();
    // example of requesting meta from IW4MAdmin
    // self RequestClientMeta( "LastServerPlayed" );
}

PlayerTrackingOnInterval() 
{
    self endon( "disconnect" );

    for ( ;; )
    {
        wait ( 120 );
        if ( IsAlive( self ) )
        {
            self SaveTrackingMetrics();
        }
    }
}

MonitorClientEvents()
{
    level endon( "disconnect" );
    self endon( "disconnect" );
    
    for ( ;; ) 
    {
        level waittill( level.eventTypes.localClientEvent, client );
 
        if ( level.iw4adminIntegrationDebug == 1 )
        {
            IPrintLn( "Processing Event " + client.event.type + "-" + client.event.subtype );
        }
        
        eventHandler = level.eventCallbacks[client.event.type];

        if ( isDefined( eventHandler ) )
        {
            client [[eventHandler]]( client.event );
        }
        
        client.eventData = [];
    }
}

//////////////////////////////////
// Helper Methods
//////////////////////////////////

RegisterClientCommands() 
{
    AddClientCommand( "GiveWeapon",     true,  ::GiveWeaponImpl );
    AddClientCommand( "TakeWeapons",    true,  ::TakeWeaponsImpl );
    AddClientCommand( "SwitchTeams",    true,  ::TeamSwitchImpl );
    AddClientCommand( "Hide",           false, ::HideImpl );
    AddClientCommand( "Unhide",         false, ::UnhideImpl );
    AddClientCommand( "Alert",          true,  ::AlertImpl );
    AddClientCommand( "Goto",           false, ::GotoImpl );
    AddClientCommand( "Kill",           true,  ::KillImpl );
    AddClientCommand( "SetSpectator",   true,  ::SetSpectatorImpl );
    AddClientCommand( "NightMode",      false, ::NightModeImpl ); //This really should be a level command
    AddClientCommand( "LockControls",   true,  ::LockControlsImpl ); 
    AddClientCommand( "UnlockControls", true,  ::UnlockControlsImpl );
    AddClientCommand( "PlayerToMe",     true,  ::PlayerToMeImpl );
    AddClientCommand( "NoClip",         false, ::NoClipImpl );
    AddClientCommand( "NoClipOff",      false, ::NoClipOffImpl );
}

InitializeGameMethods() 
{
    level.overrideMethods = [];
    level.overrideMethods["god"] = ::_god;
    level.overrideMethods["noclip"] = ::UnsupportedFunc;
    
    if ( isDefined( ::God ) )
    {
        level.overrideMethods["god"] = ::God;
    }
    
    if ( isDefined( ::NoClip ) )
    {
        level.overrideMethods["noclip"] = ::NoClip;
    }
}

UnsupportedFunc()
{ 
    self IPrintLnBold( "Function isn't supported!" );
}

RequestClientMeta( metaKey )
{
    getClientMetaEvent = BuildEventRequest( true, level.eventTypes.clientDataRequested, "Meta", self, metaKey );
    level thread QueueEvent( getClientMetaEvent, level.eventTypes.clientDataRequested, self );
}

RequestClientBasicData()
{
    getClientDataEvent = BuildEventRequest( true, level.eventTypes.clientDataRequested, "None", self, "" );
    level thread QueueEvent( getClientDataEvent, level.eventTypes.clientDataRequested, self );
}

IncrementClientMeta( metaKey, incrementValue, clientId )
{
    SetClientMeta( metaKey, incrementValue, clientId, "increment" );
}

DecrementClientMeta( metaKey, decrementValue, clientId )
{
    SetClientMeta( metaKey, decrementValue, clientId, "decrement" );
}

GenerateJoinTeamString( isSpectator ) 
{
    team = self.team;

    if ( IsDefined( self.joining_team ) )
    {
        team = self.joining_team;
    }
    else
    {
        if ( isSpectator || !IsDefined( team ) ) 
        {
            team = "spectator";
        }
    }

    guid = self GetXuid();

    if ( guid == "0" )
    {
        guid = self.guid;
    }

    if ( !IsDefined( guid ) || guid == "0" )
    {
        guid = "undefined";
    }

    return "JT;" + guid + ";" + self getEntityNumber() + ";" + team + ";" + self.name + "\n";
}

SetClientMeta( metaKey, metaValue, clientId, direction )
{
    data = "key=" + metaKey + "|value=" + metaValue;
    clientNumber = -1;

    if ( IsDefined ( clientId ) )
    {
        data = data + "|clientId=" + clientId;
        clientNumber = -1;
    }

    if ( IsDefined( direction ) )
    {
        data = data + "|direction=" + direction;
    }

    if ( IsPlayer( self ) )
    {
        clientNumber = self getEntityNumber();
    }

    setClientMetaEvent = BuildEventRequest( true, level.eventTypes.setClientDataRequested, "Meta", clientNumber, data );
    level thread QueueEvent( setClientMetaEvent, level.eventTypes.setClientDataRequested, self );
}

SaveTrackingMetrics()
{
    if ( level.iw4adminIntegrationDebug == 1 )
    {
        IPrintLn( "Saving tracking metrics for " + self.persistentClientId );
    }
    
    if ( !IsDefined( self.lastShotCount ) )
    {
        self.lastShotCount = 0;
    }

    currentShotCount = self getPlayerStat( "mostshotsfired" );
    change = currentShotCount - self.lastShotCount;
    self.lastShotCount = currentShotCount;

    if ( level.iw4adminIntegrationDebug == 1 )
    {
        IPrintLn( "Total Shots Fired increased by " + change );
    }

    if ( !IsDefined( change ) )
    {
        change = 0;
    }
    
    if ( change == 0 )
    {
        return;
    }

    IncrementClientMeta( "TotalShotsFired", change, self.persistentClientId );

}

BuildEventRequest( responseExpected, eventType, eventSubtype, entOrId, data ) 
{
    if ( !isDefined( data ) )
    {
        data = "";
    }
    
    if ( !isDefined( eventSubtype ) )
    {
        eventSubtype = "None";
    }

    if ( IsPlayer( entOrId ) )
    {
        entOrId = entOrId getEntityNumber();
    }
    
    request = "0";
    
    if ( responseExpected ) 
    {
        request = "1";
    }
  
    request = request + ";" + eventType + ";" + eventSubtype + ";" + entOrId + ";" + data;
    return request;
}

MonitorBus()
{
    level endon( "game_ended" );
    
    for( ;; )
    {
        wait ( 0.1 );
        
        // check to see if IW4MAdmin is ready to receive more data
        if ( getDvar( level.eventBus.inVar ) == "" ) 
        {
            level notify( "bus_ready" );
        }
        
        eventString = getDvar( level.eventBus.outVar );
        
        if ( eventString == "" ) 
        {
            continue;
        }
        if ( level.iw4adminIntegrationDebug == 1 )
        {
            IPrintLn( "-> " + eventString );
        }
        
        NotifyClientEvent( strtok( eventString, ";" ) );
        
        SetDvar( level.eventBus.outVar, "" );
    }
}

QueueEvent( request, eventType, notifyEntity ) 
{
    level endon( "disconnect" );

    start = GetTime();
    maxWait = level.eventBus.timeout * 1000; // 30 seconds
    timedOut = "";
   
    while ( GetDvar( level.eventBus.inVar ) != "" && ( GetTime() - start ) < maxWait )
    {
        level waittill_notify_or_timeout( "bus_ready", 1 );
        
        if ( GetDvar( level.eventBus.inVar ) != "" )
        {
            if ( level.iw4adminIntegrationDebug == 1 )
            {
                IPrintLn( "A request is already in progress..." );
            }
            timedOut = "set";
            continue;
        }
        
        timedOut = "unset";
    }
   
    if ( timedOut == "set")
    {
        if ( level.iw4adminIntegrationDebug == 1 )
        {
            IPrintLn( "Timed out waiting for response..." );
        }
        
        if ( IsDefined( notifyEntity) )
        {
            notifyEntity NotifyClientEventTimeout( eventType );
        }

        return;
    }
    
    if ( level.iw4adminIntegrationDebug == 1 )
    {
        IPrintLn("<- " + request);
    }
    
    SetDvar( level.eventBus.inVar, request );
}

ParseDataString( data ) 
{
    dataParts = strtok( data, "|" );
    dict = [];
    
    counter = 0;
    foreach ( part in dataParts )
    {
        splitPart = strtok( part, "=" );
        key = splitPart[0];
        value = splitPart[1];
        dict[key] = value;
        dict[counter] = key;
        counter++;
    }
    
    return dict;
}

NotifyClientEventTimeout( eventType ) 
{
    // todo: make this actual eventing
    if ( eventType == level.eventTypes.clientDataRequested )
    {
        self.pers["clientData"].state = level.eventBus.timeoutKey;
    }
}

NotifyClientEvent( eventInfo )
{
    origin = getPlayerFromClientNum( int( eventInfo[3] ) );
    target = getPlayerFromClientNum( int( eventInfo[4] ) );
    
    event = spawnstruct();
    event.type = eventInfo[1];
    event.subtype = eventInfo[2];
    event.data = eventInfo[5];
    event.origin = origin;
    event.target = target;
    
    if ( level.iw4adminIntegrationDebug == 1 )
    {
        IPrintLn( "NotifyClientEvent->" + event.data );
        if( int( eventInfo[3] ) != -1 && !isDefined( origin ) )
        {
            IPrintLn( "origin is null but the slot id is " + int( eventInfo[3] ) );
        }
        if( int( eventInfo[4] ) != -1 && !isDefined( target ) )
        {
            IPrintLn( "target is null but the slot id is " + int( eventInfo[4] ) );
        }
    }

    if( isDefined( target ) )
    {
        client = event.target;
    }
    else if( isDefined( origin ) )
    {
        client = event.origin;
    }
    else
    {
        if ( level.iw4adminIntegrationDebug == 1 )
        {
            IPrintLn( "Neither origin or target are set but we are a Client Event, aborting" );
        }
        
        return;
    }
    client.event = event;

    level notify( level.eventTypes.localClientEvent, client );
}

GetPlayerFromClientNum( clientNum )
{
    if ( clientNum < 0 )
        return undefined;
    
    for ( i = 0; i < level.players.size; i++ )
    {
        if ( level.players[i] getEntityNumber() == clientNum )
        {
            return level.players[i];
        }
    }
    return undefined;
}

AddClientCommand( commandName, shouldRunAsTarget, callback, shouldOverwrite )
{
    if ( isDefined( level.clientCommandCallbacks[commandName] ) && isDefined( shouldOverwrite ) && !shouldOverwrite ) {

        return;
    }
    level.clientCommandCallbacks[commandName] = callback;
    level.clientCommandRusAsTarget[commandName] = shouldRunAsTarget == true; //might speed up things later in case someone gives us a string or number instead of a boolean
}



//////////////////////////////////
// Event Handlers
/////////////////////////////////

OnClientDataReceived( event )
{
    event.data = ParseDataString( event.data );
    clientData = self.pers[level.clientDataKey];

    if ( event.subtype == "Fail" ) 
    {
        if ( level.iw4adminIntegrationDebug == 1 )
        {
            IPrintLn( "Received fail response" );
        }
        clientData.state = level.eventBus.failKey;
        return;
    }

    if ( event.subtype == "Meta" )
    {
        if ( !isDefined( clientData.meta ) )
        {
            clientData.meta = [];
        }
        
        metaKey = event.data[0];
        clientData.meta[metaKey] = event.data[metaKey];
        
        return;
    }
    
    clientData.permissionLevel = event.data["level"];
    clientData.clientId = event.data["clientId"];
    clientData.lastConnection = event.data["lastConnection"];
    clientData.state = "complete";
    self.persistentClientId = event.data["clientId"];

    self thread DisplayWelcomeData();
}

OnExecuteCommand( event ) 
{
    data = ParseDataString( event.data );
    response = "";

    command = level.clientCommandCallbacks[event.subtype];
    runAsTarget = level.clientCommandRusAsTarget[event.subtype];
    executionContextEntity = event.origin;
    if ( runAsTarget ) {
        executionContextEntity = event.target;
    }
    if ( isDefined( command ) ) {
        response = executionContextEntity [[command]]( event, data );
    }
    else if ( level.iw4adminIntegrationDebug == 1 )
    {
        IPrintLn( "Unkown Client command->" +  event.subtype);
    }
    
    // send back the response to the origin, but only if they're not the target
    if ( response != "" && IsPlayer( event.origin ) && event.origin != event.target ) 
    {
        event.origin IPrintLnBold( response );
    }
}

OnSetClientDataCompleted( event )
{
    // IW4MAdmin let us know it persisted (success or fail)
    if ( level.iw4adminIntegrationDebug == 1 )
    {
        IPrintLn( "Set Client Data -> subtype = " + event.subType + " status = " + event.data["status"] );
    }
}

//////////////////////////////////
// Command Implementations
/////////////////////////////////

GiveWeaponImpl( event, data )
{
    if ( !IsAlive( self ) )
    {
        return self.name + "^7 is not alive";
    }
    
    self IPrintLnBold( "You have been given a new weapon" );
    self GiveWeapon( data["weaponName"] );
    self SwitchToWeapon( data["weaponName"] );
    
    return self.name + "^7 has been given ^5" + data["weaponName"]; 
}

TakeWeaponsImpl()
{
    if ( !IsAlive( self ) )
    {
        return self.name + "^7 is not alive";
    }
    
    self TakeAllWeapons();
    self IPrintLnBold( "All your weapons have been taken" );
    
    return "Took weapons from " + self.name;
}

TeamSwitchImpl()
{
    if ( !IsAlive( self ) )
    {
        return self + "^7 is not alive";
    }
    
    team = level.allies;
    
    if ( self.team == "allies" ) 
    {
        team = level.axis;
    }
    
    self IPrintLnBold( "You are being team switched" );
    wait( 2 );
    self [[team]]();

    return self.name + "^7 switched to " + self.team;
}

LockControlsImpl()
{
    if ( !IsAlive( self ) )
    {
        return self.name + "^7 is not alive";
    }
    

    self freezeControls( true );
    self call [[level.overrideMethods["god"]]]( true );
    self Hide();

    info = [];
    info[ "alertType" ] = "Alert!";
    info[ "message" ] = "You've been frozen!";
    
    self AlertImpl( undefined, info );

    return self.name + "\'s controls are locked";
}

UnlockControlsImpl()
{
    if ( !IsAlive( self ) )
    {
        return self.name + "^7 is not alive";
    }
    
    self freezeControls( false );
    self call [[level.overrideMethods["god"]]]( false );
    self Show();

    return self.name + "\'s controls are unlocked";
}

NoClipImpl()
{
    if ( !IsAlive( self ) )
    {
        self IPrintLnBold( "You are not alive" );
        return;
    }

    self SetClientDvar( "sv_cheats", 1 );
    self SetClientDvar( "cg_thirdperson", 1 );
    self SetClientDvar( "sv_cheats", 0 );

    self call [[level.overrideMethods["god"]]]( true );
    self call [[level.overrideMethods["noclip"]]]( true );
    self Hide();

    self IPrintLnBold( "NoClip enabled" );
}

NoClipOffImpl()
{
    if ( !IsAlive( self ) )
    {
        self IPrintLnBold( "You are not alive" );
        return;
    }
    
    self SetClientDvar( "sv_cheats", 1 );
    self SetClientDvar( "cg_thirdperson", 0 );
    self SetClientDvar( "sv_cheats", 0 );

    self call [[level.overrideMethods["god"]]]( false );
    self call [[level.overrideMethods["noclip"]]]( false );
    self Show();

    self IPrintLnBold( "NoClip disabled" );
}

HideImpl()
{
    if ( !IsAlive( self ) )
    {
        self IPrintLnBold( "You are not alive" );
        return;
    }

    self SetClientDvar( "sv_cheats", 1 );
    self SetClientDvar( "cg_thirdperson", 1 );
    self SetClientDvar( "sv_cheats", 0 );

    if ( !IsDefined( self.savedHealth ) || self.health < 1000  )
    {
        self.savedHealth = self.health;
        self.savedMaxHealth = self.maxhealth;
    }

    self call [[level.overrideMethods["god"]]]( true );
    self Hide();

    self IPrintLnBold( "You are now ^5hidden ^7from other players" );
}

UnhideImpl()
{
    if ( !IsAlive( self ) )
    {
        self IPrintLnBold( "You are not alive" );
        return;
    }
    
    if ( !IsDefined( self.isHidden ) || !self.isHidden ) 
    {
        self IPrintLnBold( "You are not hidden" );
        return;
    }

    self SetClientDvar( "sv_cheats", 1 );
    self SetClientDvar( "cg_thirdperson", 0 );
    self SetClientDvar( "sv_cheats", 0 );

    self call [[level.overrideMethods["god"]]]( false );
    self Show();
    
    self IPrintLnBold( "You are now ^5visible ^7to other players" );
}

AlertImpl( event, data )
{
    if ( level.eventBus.gamename == "IW4" ) {
        self thread maps\mp\gametypes\_hud_message::oldNotifyMessage( data["alertType"], data["message"], "compass_waypoint_target", ( 1, 0, 0 ), "ui_mp_nukebomb_timer", 7.5 );
    }
    if ( level.eventBus.gamename == "IW5" ) { //IW5's notification are a bit different...
        self thread maps\mp\gametypes\_hud_message::oldNotifyMessage( data["alertType"], data["message"], undefined, ( 1, 0, 0 ), "ui_mp_nukebomb_timer", 7.5 );
    }
    return "Sent alert to " + self.name; 
}

GotoImpl( event, data )
{
    if ( IsDefined( event.target ) )
    {
        return self GotoPlayerImpl( event.target );
    }
    else
    {
        return self GotoCoordImpl( data );
    }
}

GotoCoordImpl( data )
{
    if ( !IsAlive( self ) )
    {
        self IPrintLnBold( "You are not alive" );
        return;
    }

    position = ( int(data["x"]), int(data["y"]), int(data["z"]) );
    self SetOrigin( position );
    self IPrintLnBold( "Moved to " + "("+ position[0] + "," + position[1] + "," + position[2] + ")" );
}

GotoPlayerImpl( target )
{
    if ( !IsAlive( target ) )
    {
        self IPrintLnBold( target.name + " is not alive" );
        return;
    }

    self SetOrigin( target GetOrigin() );
    self IPrintLnBold( "Moved to " + target.name );
}

PlayerToMeImpl( event )
{
    if ( !IsAlive( self ) )
    {
        return self.name + " is not alive";
    }

    self SetOrigin( event.origin GetOrigin() );
    return "Moved here " + self.name;    
}


KillImpl()
{
    if ( !IsAlive( self ) )
    {
        return self.name + " is not alive";
    }

    self Suicide();
    self IPrintLnBold( "You were killed by " + self.name );

    return "You killed " + self.name;
}

NightModeImpl()
{
    if ( !IsDefined ( level.nightModeEnabled ) )
    {
        level.nightModeEnabled = true;
    }
    else
    {
       level.nightModeEnabled = !level.nightModeEnabled;
    }

    message = "^5NightMode ^7is disabled";

    if ( level.nightModeEnabled )
    {
        message = "^5NightMode ^7is enabled";
    }

    IPrintLnBold( message );

    foreach( player in level.players )
    {
        player ToggleNightMode();
    }
}

ToggleNightMode()
{
    colorMap = 1;
    fxDraw = 1;

    if ( IsDefined( level.nightModeEnabled ) && level.nightModeEnabled )
    {
        colorMap = 0;
        fxDraw = 0;
    }

    self SetClientDvar( "sv_cheats", 1 );
    self SetClientDvar( "r_colorMap", colorMap );
    self SetClientDvar( "fx_draw", fxDraw );
    self SetClientDvar( "sv_cheats", 0 );
}

SetSpectatorImpl()
{
    if ( self.pers["team"] == "spectator" ) 
    {
        return self.name + " is already spectating";
    }
    
    self [[level.spectator]]();
    self IPrintLnBold( "You have been moved to spectator" );
    
    return self.name + " has been moved to spectator";
}

//////////////////////////////////
// Function Overrides
//////////////////////////////////

_god( isEnabled ) 
{
    if ( isEnabled == true ) 
    {
        if ( !IsDefined( self.savedHealth ) || self.health < 1000  )
        {
            self.savedHealth = self.health;
            self.savedMaxHealth = self.maxhealth;
        }
        
        self.maxhealth = 99999;
        self.health = 99999;
    }
    
    else 
    {
        if ( !IsDefined( self.savedHealth ) || !IsDefined( self.savedMaxHealth ) )
        {
            return;
        }
        
        self.health = self.savedHealth;
        self.maxhealth = self.savedMaxHealth;
    }
}
