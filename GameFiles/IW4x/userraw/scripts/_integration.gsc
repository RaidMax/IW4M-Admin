#include common_scripts\utility;
#include maps\mp\_utility;
#include maps\mp\gametypes\_hud_util;
#include maps\mp\gametypes\_playerlogic;

init()
{
    // setup default vars
    level.eventBus              = spawnstruct();
    level.eventBus.inVar        = "sv_iw4madmin_in";
    level.eventBus.outVar       = "sv_iw4madmin_out";
    level.eventBus.failKey      = "fail";
    level.eventBus.timeoutKey   = "timeout";
    level.eventBus.timeout      = 10;
    
    level.clientDataKey = "clientData";

    level.eventTypes                            = spawnstruct();
    level.eventTypes.clientDataReceived         = "ClientDataReceived";
    level.eventTypes.clientDataRequested        = "ClientDataRequested";
    level.eventTypes.executeCommandRequested    = "ExecuteCommandRequested";
                                                                             
    SetDvarIfUninitialized( level.eventBus.inVar, "" );
    SetDvarIfUninitialized( level.eventBus.outVar, "" );
    SetDvarIfUninitialized( "sv_iw4madmin_integration_enabled", 1 );
    SetDvarIfUninitialized( "sv_iw4madmin_integration_debug", 0 );
    
    // map the event type to the handler
    level.eventCallbacks = [];
    level.eventCallbacks[level.eventTypes.clientDataReceived] = ::OnClientDataReceived;
    level.eventCallbacks[level.eventTypes.executeCommandRequested] = ::OnExecuteCommand;

    // start long running tasks
    level thread PlayerWaitEvents();
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
        
        if ( !isDefined( player.pers[level.clientDataKey] ) )
        {
            player.pers[level.clientDataKey] = spawnstruct();
        }
        
        player thread OnPlayerSpawned();
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

DisplayWelcomeData()
{
    self endon( "disconnect" );

    clientData = self.pers[level.clientDataKey];
    
    if ( clientData.permissionLevel != "User" )
    { 
        self IPrintLnBold( "Welcome, your level is ^5" + clientData.permissionLevel );
    }
    wait (2.0);
    self IPrintLnBold( "You were last seen ^5" + clientData.lastConnection );
}

PlayerConnectEvents() 
{
    self endon( "disconnect" );
    
    clientData = self.pers[level.clientDataKey];
    
    // this gives IW4MAdmin some time to register the player before making the request;
    wait ( 2 );

    if ( isDefined( clientData.state ) && clientData.state == "complete" ) 
    {
        return;
    }
    
    self RequestClientBasicData();
    // example of requesting meta from IW4MAdmin
    // self RequestClientMeta( "LastServerPlayed" );
}

PlayerWaitEvents()
{
    level endon( "game_ended" );
    self endon( "disconnect" );
    
    for ( ;; ) 
    {
        level waittill( "client_event", client );
 
        if ( level.iw4adminIntegrationDebug == 1 )
        {
            self IPrintLn( "Processing Event " + client.event.type + "-" + client.event.subtype );
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

RequestClientMeta( metaKey )
{
    getClientMetaEvent = BuildEventRequest( true, level.eventTypes.clientDataRequested, "Meta", self, metaKey );
    self thread QueueEvent( getClientMetaEvent, level.eventTypes.clientDataRequested );
}

RequestClientBasicData()
{
    getClientDataEvent = BuildEventRequest( true, level.eventTypes.clientDataRequested, "None", self, "" );
    self thread QueueEvent( getClientDataEvent, level.eventTypes.clientDataRequested );
}

BuildEventRequest( responseExpected, eventType, eventSubtype, client, data ) 
{
    if ( !isDefined( data ) )
    {
        data = "";
    }
    
    if ( !isDefined( eventSubtype ) )
    {
        eventSubtype = "None";
    }
    
    request = "0";
    
    if ( responseExpected ) 
    {
        request = "1";
    }
  
    request = request + ";" + eventType + ";" + eventSubtype + ";" + client getEntityNumber() + ";" + data;
    return request;
}

MonitorBus()
{
    level endon( "game_ended" );
    
    for( ;; )
    {
        wait ( 0.25 );
        
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

QueueEvent( request, eventType ) 
{
    self endon( "disconnect" );
   
    start = GetTime();
    maxWait = level.eventBus.timeout * 1000; // 10 seconds
    timedOut = "";
   
    while ( GetDvar( level.eventBus.inVar ) != "" && ( GetTime() - start ) < maxWait )
    {
        level waittill_notify_or_timeout( "bus_ready", 1 );
        
        if ( GetDvar( level.eventBus.inVar ) != "" )
        {
            if ( level.iw4adminIntegrationDebug == 1 )
            {
                self IPrintLn( "A request is already in progress..." );
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
            self IPrintLn( "Timed out waiting for response..." );
        }
        
        NotifyClientEventTimeout( eventType );

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
    client = getPlayerFromClientNum( int( eventInfo[3] ) );
    
    event = spawnstruct();
    event.type = eventInfo[1];
    event.subtype = eventInfo[2];
    event.data = eventInfo[4];
    
    if ( level.iw4adminIntegrationDebug == 1 )
    {
        IPrintLn(event.data);
    }
    
    client.event = event;

    level notify( "client_event", client );
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
        if ( !isDefined( clientData["meta"] ) )
        {
            clientData.meta = [];
        }
        
        metaKey = event.data[0];
        clientData["meta"][metaKey] = event.data[metaKey];
        
        return;
    }
    
    clientData.permissionLevel = event.data["level"];
    clientData.lastConnection = event.data["lastConnection"];
    clientData.state = "complete";

    self thread DisplayWelcomeData();
}

OnExecuteCommand( event ) 
{
    data = ParseDataString( event.data );
    switch ( event.subtype ) 
    {
        case "GiveWeapon":
            self GiveWeaponImpl( data );
            break;
        case "TakeWeapons":
            self TakeWeaponsImpl();
            break;
        case "SwitchTeams":
            self TeamSwitchImpl();
            break;
        case "Hide":
            self HideImpl();
            break;
        case "Unhide":
            self UnhideImpl();
            break;
        case "Alert":
            self AlertImpl( data );
            break;
    }
}

//////////////////////////////////
// Command Implementations
/////////////////////////////////

GiveWeaponImpl( data )
{
    if ( IsAlive( self ) ) 
    {
        self IPrintLnBold( "You have been given a new weapon" );
        self GiveWeapon( data["weaponName"] );
        self SwitchToWeapon( data["weaponName"] );
    }
}

TakeWeaponsImpl()
{
    if ( IsAlive( self ) )
    {
        self TakeAllWeapons();
        self IPrintLnBold( "All your weapons have been taken" );
    }
}

TeamSwitchImpl()
{
    if ( self.team == "allies" ) 
    {
        self [[level.axis]]();
    }
    else
    {
        self [[level.allies]]();
    }
}

HideImpl()
{
    if ( IsAlive( self ) )
    {
        self Hide();
        self IPrintLnBold( "You are now hidden" );
    }
}

UnhideImpl()
{
    if ( IsAlive( self ) )
    {
        self Show();
        self IPrintLnBold( "You are now visible" );
    }
}

AlertImpl( data )
{
    self thread maps\mp\gametypes\_hud_message::oldNotifyMessage( data["alertType"], data["message"], "compass_waypoint_target", ( 1, 0, 0 ), "ui_mp_nukebomb_timer", 7.5 );
}
