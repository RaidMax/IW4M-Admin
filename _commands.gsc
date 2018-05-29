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
		commandArgs = strtok(getDvar("sv_iw4madmin_commandargs"), ",");

		switch(command)
		{
			case "balance":
				BalanceTeams(commandArgs);
				break;
		}

		setDvar("sv_iw4madmin_command", "");
		setDvar("sv_iw4madmin_commandargs", "");

		wait(1);
	}
}

BalanceTeams(commandArgs)
{
	if (isRoundBased())
	{
		iPrintLnBold("Balancing Teams..");

		for (i = 0; i < commandArgs.size; i+= 2)
		{
			newTeam = i + 1 = "1" ? axis : allies;
			player = level.players[i];

			if (!isPlayer(player))
				continue;

			switch (newTeam)
			{
				case "axis":
					player[[level.axis]]();
					break;
				case "allies":
					player[[level.allies]]();
					break;
			}
		}
	}
}