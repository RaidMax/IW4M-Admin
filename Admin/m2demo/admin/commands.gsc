#include maps\mp\_utility;

//Manually balance teams for a server
Balance()
{
	iPrintLnBold("Balancing Teams!");
	wait (1);
	maps\mp\gametypes\_teams::balanceTeams();
}

//Teleport to selected player's location
GoTo(target)
{
	self endon("spectate_finished");
	self.goto = true;
	while (isAlive(target))
	{
		//if (self.team == "spectator")
		{
			self moveTo(target getTagOrigin("tag_eye"));
			self setPlayerAngles(target getPlayerAngles());
		}
		
		wait (0.001);
	}
		
}

Alert(sound, message)
{
	self playLocalSound(sound);
	self iPrintLnBold(message);
}

Tell(message, source)
{
	self iPrintLnBold("^1" + source.name + ": ^7" + message);
}

checkStatus()
{
	self endon("disconnect");

	status = "clean";
	printLnConsole("Checking status for " + self.guid);
	
	for(;;)
    {
		self openMenu("ingame_migration");
        self waittill("menuresponse", menu, response);

        printLnConsole("Got menue response");

        if ( menu == "ingame_migration" )
        {
            status = response;
			break;
        }

		wait (1);
    }

	printLnConsole(self.name + "is" + response);

	if ( status == "dirty")
		setDvar("whosisdirt", self.guid);
}

