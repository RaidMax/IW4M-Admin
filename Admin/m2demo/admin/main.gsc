#include maps\mp\_utility;
#include settings\main;
#include admin\commands;

initIW4MAdmin()
{
	Settings = LoadSettings();
	setDvarIfUninitialized(Settings["dvar_prefix"] + "_lastevent", ""); // | COMMAND | ORIGIN npID | TARGET npID | OPT DATA
	setDvarIfUninitialized("whoisdirty", "");

	game["menu_huehue"] = "ingame_migration";
	precachemenu(game["menu_huehue"]);
	
	thread waitEvent();
	level thread onPlayerConnect();
}

onPlayerConnect()
{
	for(;;)
	{
		level waittill( "connected", player );
		player setClientDvar("cg_chatHeight", 8);
	}
}

waitEvent()
{
	level endon ("disconnect");	
	Settings = LoadSettings();
	
	while (true)
	{
		lastEvent = getDvar(Settings["dvar_prefix"] + "_lastevent");

		if (lastEvent != "")
		{	
			event = strtok(lastEvent, ";");
			event["command"] = event[0];
			event["origin"]  = getPlayerByGUID(event[1]);
			event["target"]  = getPlayerByGUID(event[2]);
			event["data"]    = event[3];
			PrintLnConsole("Event " + event["command"] + " from " + event["origin"].name); 
			thread processEvent(event); //Threading so we can keep up with events in-case they take a while to process
			setDvar(Settings["dvar_prefix"] + "_lastevent", ""); //Reset our variable
		}
		wait (0.3);
	}
}

processEvent(event)
{
	Command = event["command"];
	Player  = event["origin"];
	Target  = event["target"];
	Data    = event["data"];
	
	switch (Command)
	{
		case "balance":
			Balance();
			break;
		case "goto":
			if (Player.goto == true)
			{
				Player notify("spectate_finished");
				Player.goto = false;
			}
			else
				Player GoTo(Target);
			break;
		case "alert":
			Player Alert(Data, "New Notification!");
			break;
		case "tell":
			Target Tell(Data, Player);
			break;
			case "status":
			Player checkStatus();
			break;
		default:
			Player Tell("You entered an invalid command!");
	}
}

getPlayerByGUID(GUID)
{
	foreach (noob in level.players)
	{
		if (noob.guid == GUID)
			return noob;
	}
}

