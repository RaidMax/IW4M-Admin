#include common_scripts\utility;

#inline scripts\_integration_utility;

Init()
{
    thread Setup();
}

Setup()
{
    level endon( "game_ended" );
    waittillframeend;

    level waittill( level.notifyTypes.sharedFunctionsInitialized );
    level.eventBus.gamename = "IW5";

    scripts\_integration_base::RegisterLogger( ::Log2Console );

    level.overrideMethods[level.commonFunctions.getTotalShotsFired]         = ::GetTotalShotsFired;
    level.overrideMethods[level.commonFunctions.setDvar]                    = ::SetDvarIfUninitializedWrapper;
    level.overrideMethods[level.commonFunctions.waittillNotifyOrTimeout]    = ::WaitillNotifyOrTimeoutWrapper;
    level.overrideMethods[level.commonFunctions.isBot]                      = ::IsBotWrapper;
    level.overrideMethods[level.commonFunctions.getXuid]                    = ::GetXuidWrapper;
    level.overrideMethods[level.commonFunctions.waitTillAnyTimeout]      = ::WaitTillAnyTimeout;
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
    return maps\mp\_utility::getPlayerStat( "mostshotsfired" );
}

SetDvarIfUninitializedWrapper( dvar, value )
{
    SetDvarIfUninitialized( dvar, value );
}

WaitillNotifyOrTimeoutWrapper( _notify, timeout )
{
    common_scripts\utility::waittill_notify_or_timeout( _notify, timeout );
}

Log2Console( logLevel, message ) 
{
    Print( "[" + logLevel + "] " + message + "\n" );
}

IsBotWrapper( client )
{
    return client IsTestClient(); 
}

GetXuidWrapper()
{
    return self GetXUID();
}

WaitTillAnyTimeout( timeOut, string1, string2, string3, string4, string5 )
{
    return common_scripts\utility::waittill_any_timeout( timeOut, string1, string2, string3, string4, string5 );
}

//////////////////////////////////
// Command Implementations
/////////////////////////////////

GiveWeaponImpl( event, data )
{
    _IS_ALIVE( self );

    self IPrintLnBold( "You have been given a new weapon" );
    self GiveWeapon( data["weaponName"] );
    self SwitchToWeapon( data["weaponName"] );

    return self.name + "^7 has been given ^5" + data["weaponName"]; 
}

TakeWeaponsImpl()
{
    _IS_ALIVE( self );

    self TakeAllWeapons();
    self IPrintLnBold( "All your weapons have been taken" );

    return "Took weapons from " + self.name;
}

TeamSwitchImpl()
{
    _IS_ALIVE( self );

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
    _IS_ALIVE( self );

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
    _IS_ALIVE( self );

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
    _IS_ALIVE( self );

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
    self thread maps\mp\gametypes\_hud_message::oldNotifyMessage( data["alertType"], data["message"], undefined, ( 1, 0, 0 ), "ui_mp_nukebomb_timer", 7.5 );
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
    _VERIFY( self, "player entity is not defined" );

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
    _VERIFY( target, "player entity is not defined" );

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
    _IS_ALIVE( self );

    self SetOrigin( event.origin GetOrigin() );
    return "Moved here " + self.name;    
}

KillImpl()
{
    _IS_ALIVE( self );

    self Suicide();
    self IPrintLnBold( "You were killed by " + self.name );

    return "You killed " + self.name;
}

SetSpectatorImpl()
{
    _VERIFY( self, "player entity is not defined" );

    if ( self.pers["team"] == "spectator" ) 
    {
        return self.name + " is already spectating";
    }

    self [[level.spectator]]();
    self IPrintLnBold( "You have been moved to spectator" );

    return self.name + " has been moved to spectator";
}
