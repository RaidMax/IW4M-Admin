#include common_scripts\utility;

Init()
{
    thread Setup();
}

Setup()
{
    level endon( "game_ended" );
    
    // it's possible that the notify type has not been defined yet so we have to hard code it 
    level waittill( "SharedFunctionsInitialized" );
    level.eventBus.gamename = "IW5";
    
    scripts\_integration_base::RegisterLogger( ::Log2Console );
    
    level.overrideMethods["GetTotalShotsFired"] = ::GetTotalShotsFired;
    level.overrideMethods["SetDvarIfUninitialized"] = ::_SetDvarIfUninitialized;
    level.overrideMethods["waittill_notify_or_timeout"] = ::_waittill_notify_or_timeout;
    level.overrideMethods[level.commonFunctions.isBot] = ::IsTestClient;
    level.overrideMethods[level.commonFunctions.getXuid] = ::_GetXUID;
    
    RegisterClientCommands();
    
    _SetDvarIfUninitialized( "sv_iw4madmin_autobalance", 0 );
    
    level notify( level.notifyTypes.gameFunctionsInitialized );
    
    if ( GetDvarInt( "sv_iw4madmin_integration_enabled" ) != 1 )
    {
        return;
    }
    
    level thread OnPlayerConnect();
}

OnPlayerConnect()
{
    level endon ( "game_ended" );

    for ( ;; )
    {
        level waittill( "connected", player );
        
        if ( player call [[ level.overrideMethods[ level.commonFunctions.isBot ] ]]() ) 
        {
            // we don't want to track bots
            continue;    
        }
        
        player thread SetPersistentData();
        player thread WaitForClientEvents();
    }
}

RegisterClientCommands() 
{
    scripts\_integration_base::AddClientCommand( "GiveWeapon",     true,  ::GiveWeaponImpl );
    scripts\_integration_base::AddClientCommand( "TakeWeapons",    true,  ::TakeWeaponsImpl );
    scripts\_integration_base::AddClientCommand( "SwitchTeams",    true,  ::TeamSwitchImpl );
    scripts\_integration_base::AddClientCommand( "Hide",           false, ::HideImpl );
    scripts\_integration_base::AddClientCommand( "Alert",          true,  ::AlertImpl );
    scripts\_integration_base::AddClientCommand( "Goto",           false, ::GotoImpl );
    scripts\_integration_base::AddClientCommand( "Kill",           true,  ::KillImpl );
    scripts\_integration_base::AddClientCommand( "SetSpectator",   true,  ::SetSpectatorImpl );
    scripts\_integration_base::AddClientCommand( "LockControls",   true,  ::LockControlsImpl ); 
    scripts\_integration_base::AddClientCommand( "PlayerToMe",     true,  ::PlayerToMeImpl );
    scripts\_integration_base::AddClientCommand( "NoClip",         false, ::NoClipImpl );
}

WaitForClientEvents()
{
    self endon( "disconnect" );
    
    // example of requesting a meta value
    lastServerMetaKey = "LastServerPlayed";
    // self scripts\_integration_base::RequestClientMeta( lastServerMetaKey );

    for ( ;; )
    {
        self waittill( level.eventTypes.localClientEvent, event );

	    scripts\_integration_base::LogDebug( "Received client event " + event.type );
        
        if ( event.type == level.eventTypes.clientDataReceived && event.data[0] == lastServerMetaKey )
        {
            clientData = self.pers[level.clientDataKey];
            lastServerPlayed = clientData.meta[lastServerMetaKey];
        }
    }
}

GetTotalShotsFired()
{
    return maps\mp\_utility::getPlayerStat( "mostshotsfired" );
}

_SetDvarIfUninitialized( dvar, value )
{
    SetDvarIfUninitialized( dvar, value );
}

_waittill_notify_or_timeout( _notify, timeout )
{
    common_scripts\utility::waittill_notify_or_timeout( _notify, timeout );
}

Log2Console( logLevel, message ) 
{
    Print( "[" + logLevel + "] " + message + "\n" );
}

_GetXUID()
{
    return self GetXUID();
}

//////////////////////////////////
// GUID helpers
/////////////////////////////////

SetPersistentData() 
{
    self endon( "disconnect" );
    
    guidHigh = self GetPlayerData( "bests", "none" ); 
    guidLow = self GetPlayerData( "awards", "none" );
    persistentGuid = guidHigh + "," + guidLow;
    guidIsStored = guidHigh != 0 && guidLow != 0;
     
    if ( guidIsStored )
    {
        // give IW4MAdmin time to collect IP
        wait( 15 );
        scripts\_integration_base::LogDebug( "Uploading persistent guid " + persistentGuid );
        scripts\_integration_base::SetClientMeta( "PersistentClientGuid", persistentGuid );
        return;
    }
    
    guid = self SplitGuid();
    
    scripts\_integration_base::LogDebug( "Persisting client guid " + guidHigh + "," + guidLow );
    
    self SetPlayerData( "bests", "none", guid["high"] );
    self SetPlayerData( "awards", "none", guid["low"] );
}

SplitGuid()
{
    guid = self GetGuid();
    
    if ( isDefined( self.guid ) )
    {
        guid = self.guid;
    }
    
    firstPart = 0;
    secondPart = 0;
    stringLength = 17;
    firstPartExp = 0;
    secondPartExp = 0;
    
    for ( i = stringLength - 1; i > 0; i-- )
    {
        char = GetSubStr( guid, i - 1, i );
        if ( char == "" ) 
        {
            char = "0";
        }
        
        if ( i > stringLength / 2 )
        {
            value = GetIntForHexChar( char );
            power = Pow( 16, secondPartExp );
            secondPart = secondPart + ( value * power );
            secondPartExp++;
        }   
        else
        {
            value = GetIntForHexChar( char );
            power = Pow( 16, firstPartExp );
            firstPart = firstPart + ( value * power );
            firstPartExp++;
        }
    }
    
    split = [];
    split["low"] = int( secondPart );
    split["high"] = int( firstPart );

    return split;
}

Pow( num, exponent )
{
    result = 1;
    while( exponent != 0 )
    {
        result = result * num;
        exponent--;
    }
    
    return result;
}

GetIntForHexChar( char )
{
    char = ToLower( char );
    // generated by co-pilot because I can't be bothered to make it more "elegant"
    switch( char )
    {
        case "0":
            return 0;
        case "1":
            return 1;
        case "2":
            return 2;
        case "3":
            return 3;
        case "4":
            return 4;
        case "5":
            return 5;
        case "6":
            return 6;
        case "7":
            return 7;
        case "8":
            return 8;
        case "9":
            return 9;
        case "a":
            return 10;
        case "b":
            return 11;
        case "c":
            return 12;
        case "d":
            return 13;
        case "e":
            return 14;
        case "f":
            return 15;
        default:
            return 0;
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

    if ( !IsDefined ( self.isControlLocked ) )
    {
        self.isControlLocked = false;
    }

    if ( !self.isControlLocked )
    {
        self freezeControls( true );
        self God();
        self Hide();

        info = [];
        info[ "alertType" ] = "Alert!";
        info[ "message" ] = "You have been frozen!";
        
        self AlertImpl( undefined, info );

        self.isControlLocked = true;
        
        return self.name + "\'s controls are locked";
    }
    else
    {
        self freezeControls( false );
        self God();
        self Show();

        self.isControlLocked = false;

        return self.name + "\'s controls are unlocked";
    }
}

NoClipImpl()
{
    if ( !IsAlive( self ) )
    {
        self IPrintLnBold( "You are not alive" );
    }
    
    if ( !IsDefined ( self.isNoClipped ) )
    {
        self.isNoClipped = false;
    }

    if ( !self.isNoClipped )
    {
        SetDvar( "sv_cheats", 1 );
        self SetClientDvar( "cg_thirdperson", 1 );
        
        self God();
        self Noclip();
        self Hide();
        SetDvar( "sv_cheats", 0 );
        
        self.isNoClipped = true;
        
        self IPrintLnBold( "NoClip enabled" );
    }
    else
    {
        SetDvar( "sv_cheats", 1 );
        self SetClientDvar( "cg_thirdperson", 0 );
        
        self God();
        self Noclip();
        self Hide();
        
        SetDvar( "sv_cheats", 0 );
        
        self.isNoClipped = false;
        
        self IPrintLnBold( "NoClip disabled" );
    }

    self IPrintLnBold( "NoClip enabled" );
}

HideImpl()
{
    if ( !IsAlive( self ) )
    {
        self IPrintLnBold( "You are not alive" );
        return;
    }
    
    if ( !IsDefined ( self.isHidden ) )
    {
        self.isHidden = false;
    }

    if ( !self.isHidden )
    {
        SetDvar( "sv_cheats", 1 );
        self SetClientDvar( "cg_thirdperson", 1 );
        
        self God();
        self Hide();
        SetDvar( "sv_cheats", 0 );
        
        self.isHidden = true;
        
        self IPrintLnBold( "Hide enabled" );
    }
    else
    {
        SetDvar( "sv_cheats", 1 );
        self SetClientDvar( "cg_thirdperson", 0 );
        
        self God();
        self Show();
        SetDvar( "sv_cheats", 0 );
        
        self.isHidden = false;
        
        self IPrintLnBold( "Hide disabled" );
    }
}

AlertImpl( event, data )
{
    if ( level.eventBus.gamename == "IW5" ) {
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

    position = ( int( data["x"] ), int( data["y"] ), int( data["z"]) );
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
