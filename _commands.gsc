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
			teamNum = commandArgs[i+1];
			clientNum = commandArgs[i];
			if (teamNum == "0")
				newTeam = "allies";
			else
				newTeam = "axis";
			player = level.players[clientNum];

			if (!isPlayer(player))
				continue;

			iPrintLnBold(player.name + " " + teamNum);

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