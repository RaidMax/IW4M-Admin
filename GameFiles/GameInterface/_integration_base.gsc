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
    level.commonFunctions.getInboundData            = "GetInboundData";
    level.commonFunctions.getOutboundData           = "GetOutboundData";
    level.commonFunctions.setInboundData            = "SetInboundData";
    level.commonFunctions.setOutboundData           = "SetOutboundData";

    level.overrideMethods = [];
    level.overrideMethods[level.commonFunctions.setDvar]                = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getPlayerFromClientNum] = ::_GetPlayerFromClientNum;
    level.overrideMethods[level.commonFunctions.getInboundData]  = ::_GetInboundData;
    level.overrideMethods[level.commonFunctions.getOutboundData] = ::_GetOutboundData;
    level.overrideMethods[level.commonFunctions.setInboundData]  = ::_SetInboundData;
    level.overrideMethods[level.commonFunctions.setOutboundData] = ::_SetOutboundData;

    level.busMethods = [];
    level.busMethods[level.commonFunctions.getInboundData]  = ::_GetInboundData;
    level.busMethods[level.commonFunctions.getOutboundData] = ::_GetOutboundData;
    level.busMethods[level.commonFunctions.setInboundData]  = ::_SetInboundData;
    level.busMethods[level.commonFunctions.setOutboundData] = ::_SetOutboundData;

    level.commonKeys = spawnstruct();
    level.commonKeys.enabled  = "sv_iw4madmin_integration_enabled";
    level.commonKeys.busMode  = "sv_iw4madmin_integration_busmode";
    level.commonKeys.busDir   = "sv_iw4madmin_integration_busdir";
    level.eventBus.inLocation = "";
    level.eventBus.outLocation = "";

    level.notifyTypes                                   = spawnstruct();
    level.notifyTypes.gameFunctionsInitialized          = "GameFunctionsInitialized";
    level.notifyTypes.sharedFunctionsInitialized        = "SharedFunctionsInitialized";
    level.notifyTypes.integrationBootstrapInitialized   = "IntegrationBootstrapInitialized";

    level.clientDataKey = "clientData";

    level.eventTypes                            = spawnstruct();
    level.eventTypes.eventAvailable             = "EventAvailable";
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
    _SetDvarIfUninitialized( level.commonKeys.busMode, "rcon" );
    _SetDvarIfUninitialized( level.commonKeys.busdir, "" );
    _SetDvarIfUninitialized( "sv_iw4madmin_integration_debug", 0 );
    _SetDvarIfUninitialized( "GroupSeparatorChar", "" );
    _SetDvarIfUninitialized( "RecordSeparatorChar", "" );
    _SetDvarIfUninitialized( "UnitSeparatorChar", "" );

    if ( GetDvarInt( level.commonKeys.enabled ) != 1 )
    {
        return;
    }

    // start long running tasks
    thread MonitorEvents();
    thread MonitorBus();
}

MonitorEvents()
{
    level endon( level.eventTypes.gameEnd );

    for ( ;; ) 
    {
        level waittill( level.eventTypes.eventAvailable, event );
 
        LogDebug( "Processing Event " + event.type + "-" + event.subtype );

        eventHandler = level.eventCallbacks[event.type];

        if ( IsDefined( eventHandler ) )
        {
            if ( IsDefined( event.entity ) )
            {
                event.entity [[eventHandler]]( event );
            }
            else
            {
                [[eventHandler]]( event );
            }
        }

        if ( IsDefined( event.entity ) )
        {
            LogDebug( "Notify client for " + event.type );
            event.entity notify( event.type, event );
        }
        else
        {
            LogDebug( "Notify level for " + event.type );
            level notify( event.type, event );
        }
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
    assertEx( clientNum >= 0, "clientNum cannot be negative" );

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

_GetInboundData( location )
{
    return GetDvar( level.eventBus.inVar );
}

_GetOutboundData( location )
{
    return GetDvar( level.eventBus.outVar );
}

_SetInboundData( location, data )
{
    return SetDvar( level.eventBus.inVar, data );
}

_SetOutboundData( location, data )
{
    return SetDvar( level.eventBus.outVar, data );
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
        [[level.logger._logger[i]]]( LogLevel, GetSubStr( message, 0, 1000 ) );
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
    data = [];
    data["key"] = metaKey;
    data["value"] = metaValue;
    clientNumber = -1;

    if ( IsDefined ( clientId ) )
    {
        data["clientId"] = clientId;
        clientNumber = -1;
    }

    if ( IsDefined( direction ) )
    {
        data["direction"] = direction;
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

    if ( !IsDefined( entOrId ) )
    {
        entOrId = "-1";
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

    data = BuildDataString( data );
    groupSeparator = GetSubStr( GetDvar( "GroupSeparatorChar" ), 0, 1 );
    request = request + groupSeparator + eventType + groupSeparator + eventSubtype + groupSeparator + entOrId + groupSeparator + data;

    return request;
}

MonitorBus()
{
    level endon( level.eventTypes.gameEnd );

    level.eventBus.inLocation = level.eventBus.inVar + "_" + GetDvar( "net_port" );
    level.eventBus.outLocation = level.eventBus.outVar + "_" + GetDvar( "net_port" );

    [[level.overrideMethods[level.commonFunctions.SetInboundData]]]( level.eventBus.inLocation, "" );
    [[level.overrideMethods[level.commonFunctions.SetOutboundData]]]( level.eventBus.outLocation, "" );

    for( ;; )
    {
        wait ( 0.1 );

        // check to see if IW4MAdmin is ready to receive more data
        inVal = [[level.busMethods[level.commonFunctions.getInboundData]]]( level.eventBus.inLocation );

        if ( !IsDefined( inVal ) || inVal == "" )
        {
            level notify( "bus_ready" );
        }

        eventString = [[level.busMethods[level.commonFunctions.getOutboundData]]]( level.eventBus.outLocation );

        if ( !IsDefined( eventString ) || eventString == "" ) 
        {
            continue;
        }

        LogDebug( "-> " + eventString );

        groupSeparator = GetSubStr( GetDvar( "GroupSeparatorChar" ), 0, 1 );
        NotifyEvent( strtok( eventString, groupSeparator ) );

        [[level.busMethods[level.commonFunctions.SetOutboundData]]]( level.eventBus.outLocation, "" );
    }
}

QueueEvent( request, eventType, notifyEntity ) 
{
    level endon( level.eventTypes.gameEnd );

    start = GetTime();
    maxWait = level.eventBus.timeout * 1000; // 30 seconds
    timedOut = "";

    while ( [[level.busMethods[level.commonFunctions.getInboundData]]]( level.eventBus.inLocation ) != "" && ( GetTime() - start ) < maxWait )
    {
        level [[level.overrideMethods[level.commonFunctions.waittillNotifyOrTimeout]]]( "bus_ready", 1 );
        
        if ( [[level.busMethods[level.commonFunctions.getInboundData]]]( level.eventBus.inLocation ) != "" )
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
        
        [[level.busMethods[level.commonFunctions.SetInboundData]]]( level.eventBus.inLocation, "" );

        return;
    }

    LogDebug( "<- " + request );

    [[level.busMethods[level.commonFunctions.setInboundData]]]( level.eventBus.inLocation, request );
}

ParseDataString( data ) 
{
    if ( !IsDefined( data ) )
    {
        LogDebug( "No data to parse" );
        return [];
    }

    dataParts = strtok( data, GetSubStr( GetDvar( "RecordSeparatorChar" ), 0, 1 ) );
    dict = [];

    for ( i = 0; i < dataParts.size; i++ )
    {
        part = dataParts[i];
        splitPart = strtok( part, GetSubStr( GetDvar( "UnitSeparatorChar" ), 0, 1 ) );
        key = splitPart[0];
        value = splitPart[1];
        dict[key] = value;
        dict[i] = key;
    }

    return dict;
}

BuildDataString( data )
{
    if ( IsString( data ) )
    {
        return data;
    }

    dataString = "";
    keys = GetArrayKeys( data );
    unitSeparator = GetSubStr( GetDvar( "UnitSeparatorChar" ), 0, 1 );
    recordSeparator = GetSubStr( GetDvar( "RecordSeparatorChar" ), 0, 1 );

    for ( i = 0; i < keys.size; i++ )
    {
        dataString = dataString + keys[i] + unitSeparator + data[keys[i]] + recordSeparator;
    }

    return dataString;
}

NotifyClientEventTimeout( eventType ) 
{
    // todo: make this actual eventing
    if ( eventType == level.eventTypes.clientDataRequested )
    {
        self.pers["clientData"].state = level.eventBus.timeoutKey;
    }
}

NotifyEvent( eventInfo )
{
    origin = [[level.overrideMethods[level.commonFunctions.getPlayerFromClientNum]]]( int( eventInfo[3] ) );
    target = [[level.overrideMethods[level.commonFunctions.getPlayerFromClientNum]]]( int( eventInfo[4] ) );

    event = spawnstruct();
    event.type = eventInfo[1];
    event.subtype = eventInfo[2];
    event.data = ParseDataString( eventInfo[5] );
    event.origin = origin;
    event.target = target;

    if ( int( eventInfo[3] ) != -1 && !IsDefined( origin ) )
    {
        LogDebug( "origin is null but the slot id is " + int( eventInfo[3] ) );
    }
    if ( int( eventInfo[4] ) != -1 && !IsDefined( target ) )
    {
        LogDebug( "target is null but the slot id is " + int( eventInfo[4] ) );
    }

    client = event.origin;

    if ( !IsDefined( client ) )
    {
        client = event.target;
    }

    event.entity = client;
    level notify( level.eventTypes.eventAvailable, event );
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
    assertEx( isDefined( self ), "player entity is not defined");
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
    data = event.data;
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
        if ( IsDefined( executionContextEntity ) )
        {
            response = executionContextEntity thread [[command]]( event, data );
        }
        else
        {
            thread [[command]]( event );
        }
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
    LogDebug( "Set Client Data -> subtype = " + CoerceUndefined( event.subType ) + ", status = " + CoerceUndefined( event.data["status"] ) );
}

CoerceUndefined( object )
{
    if ( !IsDefined( object ) )
    {
        return "undefined";
    }

    return object;
}
