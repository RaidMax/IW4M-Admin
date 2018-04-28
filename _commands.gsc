#include maps\mp\_utility;
#include maps\mp\gametypes\_hud_util;
#include common_scripts\utility;

init()
{
	level thread WaitForCommand();
}

WaitForCommand()
{
	for(;;)
	{
		command = getDvar("sv_iw4madmin_command");
		switch(command)
		{
			case "balance":
				if (isRoundBased())
				{
					iPrintLnBold("Balancing Teams..");
					level maps\mp\gametypes\_teams::balanceTeams();
				}
				break;
		}

		setDvar("sv_iw4madmin_command", "");

		wait(1);
	}
}