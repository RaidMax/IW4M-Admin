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

Alert(sound)
{
	self playLocalSound(sound);
	self iPrintLnBold("New Report!");
}

Tell(message, source)
{
	self iPrintLnBold("^1" + source.name + ": ^7" + message);
}


