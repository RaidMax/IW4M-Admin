#include common_scripts\utility;
#include maps\mp\_utility;
#include maps\mp\gametypes\_hud_util;

Init()
{    
    level thread Setup();
}

Setup()
{
    level endon( "game_ended" );
    
    // setup default vars
    level.eventBus              = spawnstruct();
    level.eventBus.inVar        = "sv_iw4madmin_in";
    level.eventBus.outVar       = "sv_iw4madmin_out";
    level.eventBus.failKey      = "fail";
    level.eventBus.timeoutKey   = "timeout";
    level.eventBus.timeout      = 30;
    
    level.commonFunctions           = spawnstruct();
    level.commonFunctions.SetDvar   = "SetDvarIfUninitialized";
    
    level.notifyTypes                                   = spawnstruct();
    level.notifyTypes.gameFunctionsInitialized          = "GameFunctionsInitialized";
    level.notifyTypes.integrationBootstrapInitialized   = "IntegrationBootstrapInitialized";
    
    level.clientDataKey = "clientData";

    level.eventTypes                            = spawnstruct();
    level.eventTypes.localClientEvent           = "client_event";
    level.eventTypes.clientDataReceived         = "ClientDataReceived";
    level.eventTypes.clientDataRequested        = "ClientDataRequested";
    level.eventTypes.setClientDataRequested     = "SetClientDataRequested";
    level.eventTypes.setClientDataCompleted     = "SetClientDataCompleted";
    level.eventTypes.executeCommandRequested    = "ExecuteCommandRequested";
    
    level.iw4madminIntegrationDebug = 0;
    
    // map the event type to the handler
    level.eventCallbacks = [];
    level.eventCallbacks[level.eventTypes.clientDataReceived]       = ::OnClientDataReceived;
    level.eventCallbacks[level.eventTypes.executeCommandRequested]  = ::OnExecuteCommand; 
    level.eventCallbacks[level.eventTypes.setClientDataCompleted]   = ::OnSetClientDataCompleted;

    level.clientCommandCallbacks = [];
    level.clientCommandRusAsTarget = [];
    level.logger = spawnstruct();
    level.overrideMethods = [];

    level.iw4madminIntegrationDebug = GetDvarInt( "sv_iw4madmin_integration_debug" );
    InitializeLogger();
    
    wait ( 0.05 ); // needed to give script engine time to propagate notifies
    
    level notify( level.notifyTypes.integrationBootstrapInitialized );
    level waittill( level.notifyTypes.gameFunctionsInitialized );
    
    LogDebug( "Integration received notify that game functions are initialized" );
    
    _SetDvarIfUninitialized( level.eventBus.inVar, "" );
    _SetDvarIfUninitialized( level.eventBus.outVar, "" );
    _SetDvarIfUninitialized( "sv_iw4madmin_integration_enabled", 1 );
    _SetDvarIfUninitialized( "sv_iw4madmin_integration_debug", 0 );
    
    if ( GetDvarInt( "sv_iw4madmin_integration_enabled" ) != 1 )
    {
        return;
    }
    
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
    level endon ( "game_ended" );
    
    for ( ;; )
    {
        level waittill( "connected", player );
        
        if ( _IsBot( player ) ) 
        {
            // we don't want to track bots
            continue;    
        }
        
        if ( !IsDefined( player.pers[level.clientDataKey] ) )
        {
            player.pers[level.clientDataKey] = spawnstruct();
        }
        
        player thread OnPlayerSpawned();
        player thread OnPlayerJoinedTeam();
        player thread OnPlayerJoinedSpectators();
        player thread PlayerTrackingOnInterval();
    }
}

OnPlayerSpawned()
{
    self endon( "disconnect" );

    for ( ;; )
    {
        self waittill( "spawned_player" );
        self PlayerSpawnEvents();
    }
}

OnPlayerDisconnect()
{
    self endon ( "disconnect" );

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

PlayerSpawnEvents() 
{
    self endon( "disconnect" );

    clientData = self.pers[level.clientDataKey];
    
    // this gives IW4MAdmin some time to register the player before making the request;
    // although probably not necessary some users might have a slow database or poll rate
    wait ( 2 );

    if ( IsDefined( clientData.state ) && clientData.state == "complete" ) 
    {
        return;
    }
    
    self RequestClientBasicData();
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
    level endon( "game_ended" );

    for ( ;; ) 
    {
        level waittill( level.eventTypes.localClientEvent, client );
 
        LogDebug( "Processing Event " + client.event.type + "-" + client.event.subtype );
        
        eventHandler = level.eventCallbacks[client.event.type];

        if ( IsDefined( eventHandler ) )
        {
            client [[eventHandler]]( client.event );
            LogDebug( "notify client for " + client.event.type );
            client notify( level.eventTypes.localClientEvent, client.event );
        }
        
        client.eventData = [];
    }
}

//////////////////////////////////
// Helper Methods
//////////////////////////////////

_IsBot( entity )
{
    // there already is a cgame function exists as "IsBot", for IW4, but unsure what all titles have it defined,
    // so we are defining it here
    return IsDefined( entity.pers["isBot"] ) && entity.pers["isBot"];
}

_SetDvarIfUninitialized( dvarName, dvarValue )
{
    [[level.overrideMethods[level.commonFunctions.SetDvar]]]( dvarName, dvarValue );
}

// Not every game can output to console or even game log.
// Adds a very basic logging system that every
// game specific script can extend.accumulate
// Logging to dvars used as example.
InitializeLogger()
{
    level.logger._logger = [];
    RegisterLogger( ::Log2Dvar );
    RegisterLogger( ::Log2IngamePrint );
    level.logger.debug = ::LogDebug;
    level.logger.error = ::LogError;
    level.logger.warning = ::LogWarning;
}

_Log( LogLevel, message )
{
    for( i = 0; i < level.logger._logger.size; i++ )
    {
        [[level.logger._logger[i]]]( LogLevel, message );
    }
}

LogDebug( message )
{
    if ( level.iw4madminIntegrationDebug )
    {
        _Log( "debug", level.eventBus.gamename + ": " + message );
    }
}

LogError( message )
{
    _Log( "error", message );
}

LogWarning( message )
{
    _Log( "warning", message );
}

Log2Dvar( LogLevel, message )
{
    switch ( LogLevel )
    {
        case "debug":
            SetDvar( "sv_iw4madmin_last_debug", message );
            break;
        case "error":
            SetDvar( "sv_iw4madmin_last_error", message );
            break;
        case "warning":
            SetDvar( "sv_iw4madmin_last_warning", message );
            break;
    }
}

Log2IngamePrint( LogLevel, message )
{
    switch ( LogLevel )
    {
        case "debug":
            IPrintLn( "[DEBUG] " + message );
            break;
        case "error":
            IPrintLn( "[ERROR] " + message );
            break;
        case "warning":
            IPrintLn( "[WARN] " + message );
            break;
    }
}

RegisterLogger( logger )
{
    level.logger._logger[level.logger._logger.size] = logger;
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
    if ( !IsDefined( self.persistentClientId ) )
    {
        return;
    }

    LogDebug( "Saving tracking metrics for " + self.persistentClientId );
    
    if ( !IsDefined( self.lastShotCount ) )
    {
        self.lastShotCount = 0;
    }

    currentShotCount = self [[level.overrideMethods["GetTotalShotsFired"]]]();
    change = currentShotCount - self.lastShotCount;
    self.lastShotCount = currentShotCount;

    LogDebug( "Total Shots Fired increased by " + change );

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
    if ( !IsDefined( data ) )
    {
        data = "";
    }
    
    if ( !IsDefined( eventSubtype ) )
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
        LogDebug( "-> " + eventString );
        
        NotifyClientEvent( strtok( eventString, ";" ) );
        
        SetDvar( level.eventBus.outVar, "" );
    }
}

QueueEvent( request, eventType, notifyEntity ) 
{
    level endon( "game_ended" );

    start = GetTime();
    maxWait = level.eventBus.timeout * 1000; // 30 seconds
    timedOut = "";
   
    while ( GetDvar( level.eventBus.inVar ) != "" && ( GetTime() - start ) < maxWait )
    {
        level [[level.overrideMethods["waittill_notify_or_timeout"]]]( "bus_ready", 1 );
        
        if ( GetDvar( level.eventBus.inVar ) != "" )
        {
            LogDebug( "A request is already in progress..." );
            timedOut = "set";
            continue;
        }
        
        timedOut = "unset";
    }
   
    if ( timedOut == "set")
    {
        LogDebug( "Timed out waiting for response..." );
        
        if ( IsDefined( notifyEntity ) )
        {
            notifyEntity NotifyClientEventTimeout( eventType );
        }
        
        SetDvar( level.eventBus.inVar, "" );

        return;
    }
    
    LogDebug("<- " + request );
    
    SetDvar( level.eventBus.inVar, request );
}

ParseDataString( data ) 
{
    if ( !IsDefined( data ) )
    {
        LogDebug( "No data to parse" );
        return [];
    }
    
    dataParts = strtok( data, "|" );
    dict = [];
    
    for ( i = 0; i < dataParts.size; i++ )
    {
        part = dataParts[i];
        splitPart = strtok( part, "=" );
        key = splitPart[0];
        value = splitPart[1];
        dict[key] = value;
        dict[i] = key;
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
    
    LogDebug( "NotifyClientEvent->" + event.data );
    if ( int( eventInfo[3] ) != -1 && !IsDefined( origin ) )
    {
        LogDebug( "origin is null but the slot id is " + int( eventInfo[3] ) );
    }
    if ( int( eventInfo[4] ) != -1 && !IsDefined( target ) )
    {
        LogDebug( "target is null but the slot id is " + int( eventInfo[4] ) );
    }

    if ( IsDefined( target ) )
    {
        client = event.target;
    }
    else if ( IsDefined( origin ) )
    {
        client = event.origin;
    }
    else
    {
        LogDebug( "Neither origin or target are set but we are a Client Event, aborting" );
        
        return;
    }
    
    client.event = event;
    level notify( level.eventTypes.localClientEvent, client );
}

GetPlayerFromClientNum( clientNum )
{
    if ( clientNum < 0 )
    {
        return undefined;
    }
    
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
    if ( IsDefined( level.clientCommandCallbacks[commandName] ) && IsDefined( shouldOverwrite ) && !shouldOverwrite ) 
    {
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
        LogDebug( "Received fail response" );
        clientData.state = level.eventBus.failKey;
        return;
    }

    if ( event.subtype == "Meta" )
    {
        if ( !IsDefined( clientData.meta ) )
        {
            clientData.meta = [];
        }
        
        metaKey = event.data[0];
        clientData.meta[metaKey] = event.data[metaKey];

 	    LogDebug( "Meta Key=" + metaKey + ", Meta Value=" + event.data[metaKey] );
        
        return;
    }
    
    clientData.permissionLevel = event.data["level"];
    clientData.clientId = event.data["clientId"];
    clientData.lastConnection = event.data["lastConnection"];
    clientData.tag = event.data["tag"];
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
    
    if ( runAsTarget ) 
    {
        executionContextEntity = event.target;
    }
    
    if ( IsDefined( command ) ) 
    {
        response = executionContextEntity [[command]]( event, data );
    }
    else
    {
        LogDebug( "Unknown Client command->" +  event.subtype );
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
    LogDebug( "Set Client Data -> subtype = " + event.subType + " status = " + event.data["status"] );
}
