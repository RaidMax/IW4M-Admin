#include common_scripts\utility;
#include maps\mp\_utility;

Init()
{
    thread Setup();
}

Setup()
{
    level endon( "game_ended" );
    waittillframeend;

    level waittill( level.notifyTypes.sharedFunctionsInitialized );
    level.eventBus.gamename = "T4";

    scripts\_integration_base::RegisterLogger( ::Log2Console );

    level.overrideMethods[level.commonFunctions.getTotalShotsFired]      = ::GetTotalShotsFired;
    level.overrideMethods[level.commonFunctions.setDvar]                 = ::SetDvarIfUninitializedWrapper;
    level.overrideMethods[level.commonFunctions.waittillNotifyOrTimeout] = ::WaitillNotifyOrTimeoutWrapper;
    level.overrideMethods[level.commonFunctions.waitTillAnyTimeout]      = ::WaitTillAnyTimeoutWrapper;
    level.overrideMethods[level.commonFunctions.isBot]                   = ::IsBotWrapper;
    level.overrideMethods[level.commonFunctions.getXuid]                 = ::GetXuidWrapper;
    level.overrideMethods[level.commonFunctions.changeTeam]              = ::ChangeTeam;
    level.overrideMethods[level.commonFunctions.getTeamCounts]           = ::CountPlayers;
    level.overrideMethods[level.commonFunctions.getMaxClients]           = ::GetMaxClients;
    level.overrideMethods[level.commonFunctions.getTeamBased]            = ::GetTeamBased;
    level.overrideMethods[level.commonFunctions.getClientTeam]           = ::GetClientTeam;
    level.overrideMethods[level.commonFunctions.getClientKillStreak]     = ::GetClientKillStreak;
    level.overrideMethods[level.commonFunctions.backupRestoreClientKillStreakData] = ::BackupRestoreClientKillStreakData;

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
    return maps\mp\gametypes\_persistence::statGet( "total_shots" );
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
        self.oldmaxhealth = self.maxhealth;
        self.maxhealth = 99999;
        self.health = self.maxhealth;
        self.godmode = true;
    }
    else
    {
        self.godmode = false;
        if ( IsDefined( self.oldmaxhealth ) )
        {
            self.maxhealth = self.oldmaxhealth;
        }
        else
        {
            self.maxhealth = 100;
        }
        self.health = self.maxhealth;
    }
}

IsBotWrapper( client )
{
    return ( IsDefined( client.pers["isBot"] ) && client.pers["isBot"] );
}

GetXuidWrapper()
{
    return self GetGuid();
}

GetMaxClients()
{
    return GetDvarInt( "sv_maxclients" );
}

GetTeamBased()
{
    if ( IsDefined( level.teamBased ) )
    {
        return level.teamBased;
    }
    return false;
}

CountPlayers()
{
    allies = 0;
    axis = 0;

    for ( i = 0; i < level.players.size; i++ )
    {
        if ( !IsDefined( level.players[i].pers["team"] ) )
        {
            continue;
        }

        if ( level.players[i].pers["team"] == "allies" )
        {
            allies++;
        }
        else if ( level.players[i].pers["team"] == "axis" )
        {
            axis++;
        }
    }

    result = [];
    result["allies"] = allies;
    result["axis"] = axis;
    return result;
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
    if ( IsDefined( self.pers["cur_kill_streak"] ) )
    {
        return int( self.pers["cur_kill_streak"] );
    }
    return 0;
}

BackupRestoreClientKillStreakData( restore )
{
    if ( restore )
    {
        if ( IsDefined( self.pers["killstreak_backup"] ) )
        {
            self.pers["cur_kill_streak"] = self.pers["killstreak_backup"];
        }
    }
    else
    {
        if ( IsDefined( self.pers["cur_kill_streak"] ) )
        {
            self.pers["killstreak_backup"] = self.pers["cur_kill_streak"];
        }
    }
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
    if ( !IsAlive( self ) )
    {
        return self.name + "^7 is not alive";
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
    self thread maps\mp\gametypes\_hud_message::oldNotifyMessage( data["alertType"], data["message"], undefined, ( 1, 0, 0 ), "mp_killstreak_radar", 7.5 );
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
    if ( self.pers["team"] == "spectator" )
    {
        return self.name + " is already spectating";
    }

    self [[level.spectator]]();
    self IPrintLnBold( "You have been moved to spectator" );

    return self.name + " has been moved to spectator";
}
