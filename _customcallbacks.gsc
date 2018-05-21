#include maps\mp\_utility;
#include maps\mp\gametypes\_hud_util;
#include common_scripts\utility;

init()
{
	SetDvarIfUninitialized("sv_customcallbacks", true);
	SetDvarIfUninitialized("sv_framewaittime", 0.05);
	SetDvarIfUninitialized("sv_additionalwaittime", 0.05);
	SetDvarIfUninitialized("sv_maxstoredframes", 3);
	level thread onPlayerConnect();
	level waittill("prematch_over");
	level.callbackPlayerKilled = ::Callback_PlayerKilled;
	level.callbackPlayerDamage = ::Callback_PlayerDamage;
}

onPlayerConnect(player)
{
	for(;;)
	{
		level waittill( "connected", player );	
		player thread waitForFrameThread();
	}
}

hitLocationToBone(hitloc)
{
	switch(hitloc)
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
	self endon("disconnect");
	
	self.currentAnglePosition = 0;
	self.anglePositions = [];
		
	for(;;)
	{
		self.anglePositions[self.currentAnglePosition] = self getPlayerAngles();
		wait(getDvarFloat("sv_framewaittime"));	
		self.currentAnglePosition = (self.currentAnglePosition + 1) % getDvarInt("sv_maxstoredframes");
	}
}

waitForAdditionalAngles(logString)
{
	wait(getDvarFloat("sv_additionalwaittime"));
	
	self.angleSnapshot = [];
	
	for(i = 0; i < getDvarInt("sv_maxstoredframes"); i++)
	{
		self.angleSnapshot[i] = self.anglePositions[i];
	}

	currentPos = self.currentAnglePosition;
	anglesStr = "";
	
	i = 0;
	
	if (currentPos < getDvarInt("sv_maxstoredframes") - 1)
	{
		i = currentPos + 1;
	}
	
	while(i != currentPos)
	{
		anglesStr += self.angleSnapshot[i] + ":";
		i = (i + 1) % getDvarInt("sv_maxstoredframes");
	}
	
	logPrint(logString + ";" + anglesStr + "\n"); 
}

Process_Hit(type, attacker, sHitLoc, sMeansOfDeath, iDamage, sWeapon)
{
	victim = self;
	_attacker = attacker;
	if (!isPlayer(attacker) && isDefined(attacker.owner))
		_attacker = attacker.owner;
	else if(!isPlayer(attacker) && sMeansOfDeath == "MOD_FALLING")
		_attacker = victim;
	
	location = victim GetTagOrigin(hitLocationToBone(sHitLoc));
	isKillstreakKill = !isPlayer(attacker) || isKillstreakWeapon(sWeapon);

	logLine = "Script" + type + ";" + _attacker.guid + ";" + victim.guid + ";" + _attacker GetTagOrigin("tag_eye") + ";" + location + ";" + iDamage + ";" + sWeapon + ";" + sHitLoc + ";" + sMeansOfDeath + ";" + _attacker getPlayerAngles() + ";" + gettime() + ";" + isKillstreakKill + ";" +  _attacker playerADS();
	attacker thread waitForAdditionalAngles(logLine);
}

Callback_PlayerDamage( eInflictor, attacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, psOffsetTime )
{
	if (level.teamBased && isDefined( attacker ) && ( self != attacker ) && isDefined( attacker.team ) && ( self.pers[ "team" ] == attacker.team ))
		return;
		
		if (self.health - iDamage > 0)
		{
			self Process_Hit("Damage", attacker, sHitLoc, sMeansOfDeath, iDamage, sWeapon);
		}
	self maps\mp\gametypes\_damage::Callback_PlayerDamage(eInflictor, attacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, psOffsetTime );
}

Callback_PlayerKilled( eInflictor, attacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration )
{
	Process_Hit("Kill", attacker, sHitLoc, sMeansOfDeath, iDamage, sWeapon);
	self maps\mp\gametypes\_damage::Callback_PlayerKilled( eInflictor, attacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration );
}