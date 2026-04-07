#include common_scripts\utility;

Init()
{
    thread Setup();
}

Setup()
{
    level endon( "end_game" );
    waittillframeend;

    level waittill( level.notifyTypes.sharedFunctionsInitialized );
    level.eventBus.gamename = "T4";
    level.eventTypes.gameEnd = "end_game";

    scripts\_integration_base::RegisterLogger( ::Log2Console );

    level.overrideMethods[level.commonFunctions.getTotalShotsFired]      = ::GetTotalShotsFired;
    level.overrideMethods[level.commonFunctions.setDvar]                 = ::SetDvarIfUninitializedWrapper;
    level.overrideMethods[level.commonFunctions.waittillNotifyOrTimeout] = ::WaitillNotifyOrTimeoutWrapper;
    level.overrideMethods[level.commonFunctions.waitTillAnyTimeout]      = ::WaitTillAnyTimeoutWrapper;
    level.overrideMethods[level.commonFunctions.isBot]                   = ::IsBotWrapper;
    level.overrideMethods[level.commonFunctions.getXuid]                 = ::GetXuidWrapper;
    level.overrideMethods[level.commonFunctions.getPlayerFromClientNum]  = ::_GetPlayerFromClientNum;

    RegisterClientCommands();

    level notify( level.notifyTypes.gameFunctionsInitialized );
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

GetTotalShotsFired()
{
    return 0; // ZM has no shot tracking
}

SetDvarIfUninitializedWrapper( dvar, value )
{
    if ( GetDvar( dvar ) == "" )
    {
        SetDvar( dvar, value );
        return value;
    }

    return GetDvar( dvar );
}

WaitillNotifyOrTimeoutWrapper( msg, timer )
{
    self endon( msg );
    wait( timer );
}

WaitTillAnyTimeoutWrapper( timeOut, string1, string2, string3, string4, string5 )
{
    if ( IsDefined( string1 ) )
    {
        self endon( string1 );
    }
    if ( IsDefined( string2 ) )
    {
        self endon( string2 );
    }
    if ( IsDefined( string3 ) )
    {
        self endon( string3 );
    }
    if ( IsDefined( string4 ) )
    {
        self endon( string4 );
    }
    if ( IsDefined( string5 ) )
    {
        self endon( string5 );
    }

    wait( timeOut );
    return level.eventBus.timeoutKey;
}

Log2Console( logLevel, message )
{
    println( "[" + logLevel + "] " + message );
}

God()
{
    if ( !IsDefined( self.godmode ) )
    {
        self.godmode = false;
    }

    if ( !self.godmode )
    {
        self enableInvulnerability();
        self.godmode = true;
    }
    else
    {
        self.godmode = false;
        self disableInvulnerability();
    }
}

IsBotWrapper( client )
{
    return ( IsDefined( client.pers["isBot"] ) && client.pers["isBot"] != 0 );
}

GetXuidWrapper()
{
    return self GetGuid();
}

_GetPlayerFromClientNum( clientNum )
{
    if ( clientNum < 0 )
    {
        return undefined;
    }

    players = GetPlayers();

    for ( i = 0; i < players.size; i++ )
    {
        if ( players[i] getEntityNumber() == clientNum )
        {
            return players[i];
        }
    }

    return undefined;
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

TakeWeaponsImpl( event, data )
{
    if ( !IsAlive( self ) )
    {
        return self.name + "^7 is not alive";
    }

    self TakeAllWeapons();
    self IPrintLnBold( "All your weapons have been taken" );

    return "Took weapons from " + self.name;
}

TeamSwitchImpl( event, data )
{
    // Team switching not applicable in zombies
    self IPrintLnBold( "Team switching is not available in Zombies" );
    return "Team switching is not available in Zombies";
}

LockControlsImpl( event, data )
{
    if ( !IsAlive( self ) )
    {
        return self.name + "^7 is not alive";
    }

    if ( !IsDefined( self.isControlLocked ) )
    {
        self.isControlLocked = false;
    }

    if ( !self.isControlLocked )
    {
        self freezeControls( true );
        self God();
        self Hide();

        info = [];
        info["alertType"] = "Alert!";
        info["message"] = "You have been frozen!";

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

NoClipImpl( event, data )
{
    scripts\_integration_base::LogWarning( "NoClip is not supported on T4!" );
}

HideImpl( event, data )
{
    if ( !IsAlive( self ) )
    {
        self IPrintLnBold( "You are not alive" );
        return;
    }

    if ( !IsDefined( self.isHidden ) )
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
    // T4 ZM does not have oldNotifyMessage, use IPrintLnBold instead
    self IPrintLnBold( data["message"] );
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

    position = ( int( data["x"] ), int( data["y"] ), int( data["z"] ) );
    self SetOrigin( position );
    self IPrintLnBold( "Moved to " + "(" + position[0] + "," + position[1] + "," + position[2] + ")" );
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

PlayerToMeImpl( event, data )
{
    if ( !IsAlive( self ) )
    {
        return self.name + " is not alive";
    }

    self SetOrigin( event.origin GetOrigin() );
    return "Moved here " + self.name;
}

KillImpl( event, data )
{
    if ( !IsAlive( self ) )
    {
        return self.name + " is not alive";
    }

    self Suicide();
    self IPrintLnBold( "You were killed by " + self.name );

    return "You killed " + self.name;
}

SetSpectatorImpl( event, data )
{
    // Spectator mode in zombies - just notify
    self IPrintLnBold( "Spectator switching is not standard in Zombies" );
    return "Spectator switching is not standard in Zombies";
}
