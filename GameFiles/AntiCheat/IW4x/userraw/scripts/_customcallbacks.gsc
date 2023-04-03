#include maps\mp\_utility;
#include maps\mp\gametypes\_hud_util;
#include common_scripts\utility;

init()
{
	SetDvarIfUninitialized( "sv_customcallbacks", true );
	SetDvarIfUninitialized( "sv_framewaittime", 0.05 );
	SetDvarIfUninitialized( "sv_additionalwaittime", 0.1 );
	SetDvarIfUninitialized( "sv_maxstoredframes", 12 );
	SetDvarIfUninitialized( "sv_printradarupdates", 0 );
	SetDvarIfUninitialized( "sv_printradar_updateinterval", 500 );
	SetDvarIfUninitialized( "sv_iw4madmin_url", "http://127.0.0.1:1624" );
	
	level thread onPlayerConnect();
	if (getDvarInt("sv_printradarupdates") == 1)
	{
		level thread runRadarUpdates();
	}
	
	level waittill( "prematch_over" );
	level.callbackPlayerKilled = ::Callback_PlayerKilled;
	level.callbackPlayerDamage = ::Callback_PlayerDamage;
	level.callbackPlayerDisconnect = ::Callback_PlayerDisconnect;
}

onPlayerConnect( player )
{
	for( ;; )
	{
		level waittill( "connected", player );	
		player setClientDvars( "cl_autorecord", 1,
					"cl_demosKeep", 200 );

		player thread waitForFrameThread();
		player thread waitForAttack();
	}
}

waitForAttack()
{
	self endon( "disconnect" );
	
	self notifyOnPlayerCommand( "player_shot", "+attack" );
	self.lastAttackTime = 0;
	
	for( ;; )
	{
		self waittill( "player_shot" );
		
		self.lastAttackTime = getTime();
	}
}

runRadarUpdates()
{
	interval = getDvarInt( "sv_printradar_updateinterval", 500 );

	for ( ;; )
	{
		for ( i = 0; i <= 17; i++ )
		{
			player = level.players[i];

			if ( isDefined( player ) )
			{
				payload = player.guid + ";" + player.origin + ";" + player getPlayerAngles() + ";" + player.team + ";" + player.kills + ";" + player.deaths + ";" + player.score + ";" + player GetCurrentWeapon() + ";" + player.health + ";" + isAlive(player) + ";" + player.timePlayed["total"];
				logPrint( "LiveRadar;" + payload + "\n" );
			}	
		}

		wait( interval / 1000 );
	}	
}

hitLocationToBone( hitloc )
{
	switch( hitloc )
	{
		case "helmet":
			return "j_helmet";
		case "head":
			return "j_head";
		case "neck":
			return "j_neck";
		case "torso_upper":
			return "j_spineupper";
		case "torso_lower":
			return "j_spinelower";
		case "right_arm_upper":
			return "j_shoulder_ri";
		case "left_arm_upper":
			return "j_shoulder_le";
		case "right_arm_lower":
			return "j_elbow_ri";
		case "left_arm_lower":
			return "j_elbow_le";
		case "right_hand":
			return "j_wrist_ri";
		case "left_hand":
			return "j_wrist_le";
		case "right_leg_upper":
			return "j_hip_ri";
		case "left_leg_upper":
			return "j_hip_le";
		case "right_leg_lower":
			return "j_knee_ri";
		case "left_leg_lower":
			return "j_knee_le";
		case "right_foot":
			return "j_ankle_ri";
		case "left_foot":
			return "j_ankle_le";
		default:
			return "tag_origin";
	}
}

waitForFrameThread()
{
	self endon( "disconnect" );
	
	self.currentAnglePosition = 0;
	self.anglePositions = [];

	for (i = 0; i < getDvarInt( "sv_maxstoredframes" ); i++)
	{
		self.anglePositions[i] = self getPlayerAngles();
	}
		
	for( ;; )
	{
		self.anglePositions[self.currentAnglePosition] = self getPlayerAngles();
		wait( getDvarFloat( "sv_framewaittime" ) );	
		self.currentAnglePosition = (self.currentAnglePosition + 1) % getDvarInt( "sv_maxstoredframes" );
	}
}

waitForAdditionalAngles( logString, beforeFrameCount, afterFrameCount )
{
    self endon( "disconnect" );
	currentIndex = self.currentAnglePosition;
	wait( 0.05 * afterFrameCount );
	
	self.angleSnapshot = [];
	
	for( j = 0; j < self.anglePositions.size; j++ )
	{
		self.angleSnapshot[j] = self.anglePositions[j];
	}

	anglesStr = "";
	collectedFrames = 0;
	i = currentIndex - beforeFrameCount;

	while (collectedFrames < beforeFrameCount)
	{
		fixedIndex = i;
		if (i < 0)
		{
			fixedIndex = self.angleSnapshot.size - abs(i);
		}
		anglesStr += self.angleSnapshot[int(fixedIndex)] + ":";
		collectedFrames++;
		i++;
	}

	if (i == currentIndex)
	{
		anglesStr += self.angleSnapshot[i] + ":";
		i++;
	}

	collectedFrames = 0;

	while (collectedFrames < afterFrameCount)
	{
		fixedIndex = i;
		if (i > self.angleSnapshot.size - 1)
		{
			fixedIndex = i % self.angleSnapshot.size;
		}
		anglesStr += self.angleSnapshot[int(fixedIndex)] + ":";
		collectedFrames++;
		i++;
	}

	lastAttack = getTime() - self.lastAttackTime;
	isAlive = isAlive(self);

	logPrint(logString + ";" + anglesStr + ";" + isAlive + ";" + lastAttack + "\n" ); 
}

vectorScale( vector, scale )
{
	return ( vector[0] * scale, vector[1] * scale, vector[2] * scale );
}

Process_Hit( type, attacker, sHitLoc, sMeansOfDeath, iDamage, sWeapon )
{
	if (sMeansOfDeath == "MOD_FALLING" || !isPlayer(attacker))
	{
		return;
	}

	victim = self;
	_attacker = attacker;

	if ( !isPlayer( attacker ) && isDefined( attacker.owner ) )
	{
		_attacker = attacker.owner;
	}

	else if( !isPlayer( attacker ) && sMeansOfDeath == "MOD_FALLING" )
	{
		_attacker = victim;
	}
	
	location = victim GetTagOrigin( hitLocationToBone( sHitLoc ) );
	isKillstreakKill = !isPlayer( attacker ) || isKillstreakWeapon( sWeapon );

	logLine = "Script" + type + ";" + _attacker.guid + ";" + victim.guid + ";" + _attacker GetTagOrigin("tag_eye") + ";" + location + ";" + iDamage + ";" + sWeapon + ";" + sHitLoc + ";" + sMeansOfDeath + ";" + _attacker getPlayerAngles() + ";" + int(gettime()) + ";" + isKillstreakKill + ";" +  _attacker playerADS() + ";0;0";
	attacker thread waitForAdditionalAngles( logLine, 2, 2 );
}

Callback_PlayerDamage( eInflictor, attacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, psOffsetTime )
{
	if ( self.health - iDamage > 0 )
	{
		isFriendlyFire = level.teamBased && isDefined( attacker ) && ( self != attacker ) && isDefined( attacker.team ) && ( self.pers[ "team" ] == attacker.team );
		
		if ( !isFriendlyFire )
		{
			self Process_Hit( "Damage", attacker, sHitLoc, sMeansOfDeath, iDamage, sWeapon );
		}
	}

	self maps\mp\gametypes\_damage::Callback_PlayerDamage( eInflictor, attacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, psOffsetTime );
}

Callback_PlayerKilled( eInflictor, attacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration )
{
	Process_Hit( "Kill", attacker, sHitLoc, sMeansOfDeath, iDamage, sWeapon );
	self maps\mp\gametypes\_damage::Callback_PlayerKilled( eInflictor, attacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration );
}

Callback_PlayerDisconnect()
{
	level notify( "disconnected", self );
	self maps\mp\gametypes\_playerlogic::Callback_PlayerDisconnect();
}
