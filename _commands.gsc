#include common_scripts\utility;
#include maps\mp\_utility;
#include maps\mp\gametypes\_hud_util;
#include maps\mp\gametypes\_playerlogic;

init()
{
/*
	SetDvarIfUninitialized("sv_team_balance_assignments", "");
	SetDvarIfUninitialized("sv_iw4madmin_serverid", 0);
	SetDvarIfUninitialized("sv_iw4madmin_apiurl", "http://127.0.0.1:1624/api/gsc/");
	level.apiUrl = GetDvar("sv_iw4madmin_apiurl");
	level thread WaitForCommand();
	level thread onPlayerConnect();
	level thread onPlayerDisconnect();
*/
}

onPlayerConnect()
{
	for(;;)
	{
		level waittill( "connected", player );		
		player thread onJoinedTeam();
	}
}

onPlayerDisconnect()
{
	for(;;)
	{
		level waittill( "disconnected", player );
		logPrint("player disconnected\n");
		level.players[0] SetTeamBalanceAssignments(true);
	}
}

onJoinedTeam()
{
	self endon("disconnect");
	
	for(;;)
	{
		self waittill( "joined_team" );
		self SetTeamBalanceAssignments(false);
	}
}

SetTeamBalanceAssignments(isDisconnect)
{	
	assignments = GetDvar("sv_team_balance_assignments");
	dc = "";
	if (isDisconnect)
	{
		dc = "&isDisconnect=true";
	}
	url = level.apiUrl + "GetTeamAssignments/" + self.guid + "/?teams=" + assignments + dc + "&serverId=" + GetDvar("sv_iw4madmin_serverid");
	newAssignments = GetHttpString(url);
	SetDvar("sv_team_balance_assignments", newAssignments.data);
	
	if (newAssignments.success)
	{
		BalanceTeams(strtok(newAssignments.data, ","));
	}
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
	client = getPlayerFromClientNum(clientId);

	client thread playLeaderDialogOnPlayer(sound, client.team);
	client playLocalSound(sound);
	client iPrintLnBold("^1" + alertType + ": ^3" + message);
}

GetHttpString(url)
{
    response = spawnStruct();
    response.success = false;
	response.data = undefined;
	
	logPrint("Making request to " + url + "\n");
    request = httpGet(url);
    request waittill("done", success, data);

    if(success != 0){
        logPrint("Request succeeded\n");
        response.success = true;
        response.data = data;
    }
	
    else
    {
         logPrint("Request failed\n");
    }
	
    return response;
}

BalanceTeams(commandArgs)
{
	if (level.teamBased)
	{
		printOnPlayers("^5Balancing Teams...");

		for (i = 0; i < commandArgs.size; i+= 2)
		{
			teamNum = int(commandArgs[i+1]);
			clientNum = int(commandArgs[i]);
			
			//printOnPlayers("[" + teamNum + "," + clientNum + "]");
			
			if (teamNum == 2)
			{
				newTeam = "allies";
			}
			else
			{
				newTeam = "axis";
			}
			
			player = getPlayerFromClientNum(clientNum);

			//if (!isPlayer(player))
			//	continue;

			switch (newTeam)
			{
				case "axis":
					if (player.team != "axis")
					{
						//printOnPlayers("moving " + player.name + " to axis");
						player[[level.axis]]();
					}
					break;
				case "allies":
					if (player.team != "allies")
					{
						//printOnPlayers("moving " + player.name + " to allies");
						player[[level.allies]]();
					}
					break;
			}
		}
	}
}