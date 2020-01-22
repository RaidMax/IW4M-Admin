#include common_scripts\utility;
#include maps\mp\_utility;
#include maps\mp\gametypes\_hud_util;
#include maps\mp\gametypes\_playerlogic;

init()
{
	SetDvarIfUninitialized( "sv_iw4madmin_command", "" );
	level thread WaitForCommand();
}

WaitForCommand()
{
	level endon( "game_ended" );
	
	for(;;)
	{
		commandInfo = strtok( getDvar("sv_iw4madmin_command"), ";" );
		command = commandInfo[0];
		
		switch( command )
		{
			case "alert":
				//clientId        alertType       sound           message
				SendAlert( commandInfo[1], commandInfo[2], commandInfo[3], commandInfo[4] );
				break;
			case "killplayer":
				//          clientId
				KillPlayer( commandInfo[1], commandInfo[2] );
				break;
		}

		setDvar( "sv_iw4madmin_command", "" );
		wait( 1 );
	}
}

SendAlert( clientId, alertType, sound, message )
{
	client = getPlayerFromClientNum( int( clientId ) );
	client thread playLeaderDialogOnPlayer( sound, client.team );
	client playLocalSound( sound );
	client iPrintLnBold( "^1" + alertType + ": ^3" + message );
}

KillPlayer( targetId, originId)
{
	target = getPlayerFromClientNum( int( targetId ) );
	target suicide();
	origin = getPlayerFromClientNum( int( originId ) );
	
	iPrintLnBold("^1" + origin.name + " ^7killed ^5" + target.name);
}