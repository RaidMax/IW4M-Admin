#include common_scripts\iw4x_utility;

Init()
{
    thread Setup();
}

Setup()
{
    level endon( "game_ended" );
    waittillframeend;
    
    level waittill( level.notifyTypes.sharedFunctionsInitialized );
    level.eventBus.gamename = "IW4";
    
    scripts\_integration_base::RegisterLogger( ::Log2Console );
    
    level.overrideMethods[level.commonFunctions.getTotalShotsFired]                = ::GetTotalShotsFired;
    level.overrideMethods[level.commonFunctions.setDvar]                           = ::SetDvarIfUninitializedWrapper;
    level.overrideMethods[level.commonFunctions.isBot]                             = ::IsBotWrapper;
    level.overrideMethods[level.commonFunctions.getXuid]                           = ::GetXuidWrapper;
    level.overrideMethods[level.commonFunctions.changeTeam]                        = ::ChangeTeam;
    level.overrideMethods[level.commonFunctions.getTeamCounts]                     = ::CountPlayers;
    level.overrideMethods[level.commonFunctions.getMaxClients]                     = ::GetMaxClients;
    level.overrideMethods[level.commonFunctions.getTeamBased]                      = ::GetTeamBased;
    level.overrideMethods[level.commonFunctions.getClientTeam]                     = ::GetClientTeam;
    level.overrideMethods[level.commonFunctions.getClientKillStreak]               = ::GetClientKillStreak;
    level.overrideMethods[level.commonFunctions.backupRestoreClientKillStreakData] = ::BackupRestoreClientKillStreakData;
    level.overrideMethods[level.commonFunctions.waitTillAnyTimeout]                = ::WaitTillAnyTimeout;
    level.overrideMethods[level.commonFunctions.waittillNotifyOrTimeout]           = ::WaitillNotifyOrTimeoutWrapper;

    level.overrideMethods[level.commonFunctions.getInboundData]  = ::GetInboundData;
    level.overrideMethods[level.commonFunctions.getOutboundData] = ::GetOutboundData;
    level.overrideMethods[level.commonFunctions.setInboundData]  = ::SetInboundData;
    level.overrideMethods[level.commonFunctions.setOutboundData] = ::SetOutboundData;

    RegisterClientCommands();
    
    level notify( level.notifyTypes.gameFunctionsInitialized );

    scripts\_integration_base::_SetDvarIfUninitialized( level.commonKeys.busdir, GetDvar( "fs_homepath" ) + "userraw/" + "scriptdata" );
    
    if ( GetDvarInt( level.commonKeys.enabled ) != 1 )
    {
        return;
    }
    
    thread OnPlayerConnect();
}

OnPlayerConnect()
{
    level endon ( "game_ended" );

    for ( ;; )
    {
        level waittill( "connected", player );
        
        if ( player IsTestClient() ) 
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
        self waittill( level.eventTypes.eventAvailable, event );

	    scripts\_integration_base::LogDebug( "Received client event " + event.type );
        
        if ( event.type == level.eventTypes.clientDataReceived && event.data[0] == lastServerMetaKey )
        {
            clientData = self.pers[level.clientDataKey];
            lastServerPlayed = clientData.meta[lastServerMetaKey];
        }
    }
}

GetInboundData( location )
{
    return FileRead( location );
}

GetOutboundData( location )
{
    return FileRead( location );
}

SetInboundData( location, data )
{
    FileWrite( location, data, "write" );
}

SetOutboundData( location, data )
{
    FileWrite( location, data, "write" );
}

GetMaxClients()
{
    return level.maxClients;
}

GetTeamBased()
{
    return level.teamBased;
}

CountPlayers()
{
    return maps\mp\gametypes\_teams::CountPlayers();
}

GetClientTeam()
{
    if ( IsDefined( self.pers["team"] ) && self.pers["team"] == "allies" )
    {
        return "allies";
    }
    
    else if ( IsDefined( self.pers["team"] ) && self.pers["team"] == "axis" )
    {
        return "axis";
    }
    
    else
    {
        return "none";
    }
}

GetClientKillStreak()
{
    return int( self.pers["cur_kill_streak"] );
}

BackupRestoreClientKillStreakData( restore ) 
{
    if ( restore )
    {
        foreach ( index, streakStruct in self.pers["killstreaks_backup"] )
        {
		    self.pers["killstreaks"][index] =  self.pers["killstreaks_backup"][index];
        }
    }

    else 
    {
        self.pers["killstreaks_backup"] = [];
        
        foreach ( index, streakStruct in self.pers["killstreaks"] )
        {
            self.pers["killstreaks_backup"][index] = self.pers["killstreaks"][index];
        }
    }
}

WaitTillAnyTimeout( timeOut, string1, string2, string3, string4, string5 )
{
    return common_scripts\utility::waittill_any_timeout( timeOut, string1, string2, string3, string4, string5 );
}

ChangeTeam( team )
{
    switch ( team )
    {
        case "allies":
            self [[level.allies]]();
            break;
    
        case "axis":
            self [[level.axis]]();
            break;

        case "spectator":
            self [[level.spectator]]();
            break;
    }
}

GetTotalShotsFired()
{
    return maps\mp\_utility::getPlayerStat( "mostshotsfired" );
}

WaitillNotifyOrTimeoutWrapper( _notify, timeout )
{
    common_scripts\utility::waittill_notify_or_timeout( _notify, timeout );
}

Log2Console( logLevel, message ) 
{
    PrintConsole( "[" + logLevel + "] " + message + "\n" );
}

SetDvarIfUninitializedWrapper( dvar, value )
{
    SetDvarIfUninitialized( dvar, value );
}

GetXuidWrapper()
{
    return self GetXUID();
}

IsBotWrapper( client )
{
    return client IsTestClient(); 
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
        return;
    }
    
    if ( !IsDefined ( self.isNoClipped ) )
    {
        self.isNoClipped = false;
    }

    if ( !self.isNoClipped )
    {
        self SetClientDvar( "sv_cheats", 1 );
        self SetClientDvar( "cg_thirdperson", 1 );
        self SetClientDvar( "sv_cheats", 0 );
        
        self God();

        self.clientflags |= 1; // IW4x specific

        self Hide();
        
        self.isNoClipped = true;
        
        self IPrintLnBold( "NoClip enabled" );
    }
    else
    {
        self SetClientDvar( "sv_cheats", 1 );
        self SetClientDvar( "cg_thirdperson", 0 );
        self SetClientDvar( "sv_cheats", 0 );
        
        self God();

        self.clientflags &= ~1; // IW4x specific

        self Show();
        
        self.isNoClipped = false;
        
        self IPrintLnBold( "NoClip disabled" );
    }
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
        self SetClientDvar( "sv_cheats", 1 );
        self SetClientDvar( "cg_thirdperson", 1 );
        self SetClientDvar( "sv_cheats", 0 );
        
        self God();
        self Hide();
        
        self.isHidden = true;
        
        self IPrintLnBold( "Hide enabled" );
    }
    else
    {
        self SetClientDvar( "sv_cheats", 1 );
        self SetClientDvar( "cg_thirdperson", 0 );
        self SetClientDvar( "sv_cheats", 0 );
        
        self God();
        self Show();
        
        self.isHidden = false;
        
        self IPrintLnBold( "Hide disabled" );
    }
}

AlertImpl( event, data )
{
    self thread maps\mp\gametypes\_hud_message::oldNotifyMessage( data["alertType"], data["message"], "compass_waypoint_target", ( 1, 0, 0 ), "ui_mp_nukebomb_timer", 7.5 );
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
