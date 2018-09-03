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
		commandInfo = strtok(getDvar("sv_iw4madmin_command"), ";");
		command = commandInfo[0];
		commandArgs = strtok(commandInfo[1], ",");

		switch(command)
		{
			case "balance":
				BalanceTeams(commandArgs);
				break;
			case "alert":
					//clientId        alertType       sound           message
				SendAlert(commandArgs[0], commandArgs[1], commandArgs[2], commandArgs[3]);
				break;
		}

		setDvar("sv_iw4madmin_command", "");
		wait(1);
	}
}

SendAlert(clientId, alertType, sound, message)
{
	client = level.players[int(clientId)];

	client thread playLeaderDialogOnPlayer(sound, client.team);
	client playLocalSound(sound);
	client iPrintLnBold("^1" + alertType + ": ^3" + message);
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