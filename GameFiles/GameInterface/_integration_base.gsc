#include common_scripts\utility;

Init()
{    
    thread Setup();
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
    
    level.commonFunctions                           = spawnstruct();
    level.commonFunctions.setDvar                   = "SetDvarIfUninitialized";
    level.commonFunctions.getPlayerFromClientNum    = "GetPlayerFromClientNum";
    level.commonFunctions.waittillNotifyOrTimeout   = "WaittillNotifyOrTimeout";

    level.overrideMethods = [];
    level.overrideMethods[level.commonFunctions.setDvar]                = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getPlayerFromClientNum] = ::_GetPlayerFromClientNum;

    level.commonKeys = spawnstruct();
    level.commonKeys.enabled = "sv_iw4madmin_integration_enabled";
    
    level.notifyTypes                                   = spawnstruct();
    level.notifyTypes.gameFunctionsInitialized          = "GameFunctionsInitialized";
    level.notifyTypes.sharedFunctionsInitialized        = "SharedFunctionsInitialized";
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

    level.iw4madminIntegrationDebug = GetDvarInt( "sv_iw4madmin_integration_debug" );
    InitializeLogger();
    
    wait ( 0.05 * 2 ); // needed to give script engine time to propagate notifies
    
    level notify( level.notifyTypes.integrationBootstrapInitialized );
    level waittill( level.notifyTypes.gameFunctionsInitialized );
    
    LogDebug( "Integration received notify that game functions are initialized" );
    
    _SetDvarIfUninitialized( level.eventBus.inVar, "" );
    _SetDvarIfUninitialized( level.eventBus.outVar, "" );
    _SetDvarIfUninitialized( level.commonKeys.enabled, 1 );
    _SetDvarIfUninitialized( "sv_iw4madmin_integration_debug", 0 );
    
    if ( GetDvarInt( level.commonKeys.enabled ) != 1 )
    {
        return;
    }
    
    // start long running tasks
    thread MonitorClientEvents();
    thread MonitorBus();
}

//////////////////////////////////
// Client Methods
//////////////////////////////////

MonitorClientEvents()
{
    level endon( level.eventTypes.gameEnd );

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
            client notify( client.event.type, client.event );
        }
        
        client.eventData = [];
    }
}

//////////////////////////////////
// Helper Methods
//////////////////////////////////

NotImplementedFunction( a, b, c, d, e, f ) 
{
    LogWarning( "Function not implemented" );
    if ( IsDefined ( a ) )
    {
        LogWarning( a );
    }
}

_SetDvarIfUninitialized( dvarName, dvarValue )
{
    [[level.overrideMethods[level.commonFunctions.setDvar]]]( dvarName, dvarValue );
}

_GetPlayerFromClientNum( clientNum )
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
    thread QueueEvent( getClientMetaEvent, level.eventTypes.clientDataRequested, self );
}

RequestClientBasicData()
{
    getClientDataEvent = BuildEventRequest( true, level.eventTypes.clientDataRequested, "None", self, "" );
    thread QueueEvent( getClientDataEvent, level.eventTypes.clientDataRequested, self );
}

IncrementClientMeta( metaKey, incrementValue, clientId )
{
    SetClientMeta( metaKey, incrementValue, clientId, "increment" );
}

DecrementClientMeta( metaKey, decrementValue, clientId )
{
    SetClientMeta( metaKey, decrementValue, clientId, "decrement" );
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
    thread QueueEvent( setClientMetaEvent, level.eventTypes.setClientDataRequested, self );
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
    level endon( level.eventTypes.gameEnd );

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
    level endon( level.eventTypes.gameEnd );

    start = GetTime();
    maxWait = level.eventBus.timeout * 1000; // 30 seconds
    timedOut = "";
   
    while ( GetDvar( level.eventBus.inVar ) != "" && ( GetTime() - start ) < maxWait )
    {
        level [[level.overrideMethods[level.commonFunctions.waittillNotifyOrTimeout]]]( "bus_ready", 1 );
        
        if ( GetDvar( level.eventBus.inVar ) != "" )
        {
            LogDebug( "A request is already in progress..." );
            timedOut = "set";
            continue;
        }
        
        timedOut = "unset";
    }
   
    if ( timedOut == "set" )
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
    origin = [[level.overrideMethods[level.commonFunctions.getPlayerFromClientNum]]]( int( eventInfo[3] ) );
    target = [[level.overrideMethods[level.commonFunctions.getPlayerFromClientNum]]]( int( eventInfo[4] ) );
    
    event = spawnstruct();
    event.type = eventInfo[1];
    event.subtype = eventInfo[2];
    event.data = eventInfo[5];
    event.origin = origin;
    event.target = target;
    
    if ( IsDefined( event.data ) )
    {
        LogDebug( "NotifyClientEvent->" + event.data );
    }
    
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

 	    LogDebug( "Meta Key=" + CoerceUndefined( metaKey ) + ", Meta Value=" + CoerceUndefined( event.data[metaKey] ) );
        
        return;
    }
    
    clientData.permissionLevel = event.data["level"];
    clientData.clientId = event.data["clientId"];
    clientData.lastConnection = event.data["lastConnection"];
    clientData.tag = event.data["tag"];
    clientData.performance = event.data["performance"];
    clientData.state = "complete";
    self.persistentClientId = event.data["clientId"];
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
    if ( IsDefined( response ) && response != "" && IsPlayer( event.origin ) && event.origin != event.target ) 
    {
        event.origin IPrintLnBold( response );
    }
}

OnSetClientDataCompleted( event )
{
    data = ParseDataString( event.data );
    LogDebug( "Set Client Data -> subtype = " + CoerceUndefined( event.subType ) + ", status = " + CoerceUndefined( data["status"] ) );
}

CoerceUndefined( object )
{
    if ( !IsDefined( object ) )
    {
        return "undefined";
    }

    return object;
}
