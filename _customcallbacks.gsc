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
	level.callbackPlayerDisconnect = ::Callback_PlayerDisconnect;
	
	level.playerTags = [];
	level.playerTags[0] = "j_head";
	level.playerTags[1] = "j_neck";
	level.playerTags[2] = "j_spineupper";
	level.playerTags[3] = "j_spinelower";
	level.playerTags[4] = "j_shoulder_ri";
	level.playerTags[5] = "j_shoulder_le";
	level.playerTags[6] = "j_elbow_ri";
	level.playerTags[7] = "j_elbow_le";
	level.playerTags[8] = "j_wrist_ri";
	level.playerTags[9] = "j_wrist_le";
	level.playerTags[10] = "j_hip_ri";
	level.playerTags[11] = "j_hip_le";
	level.playerTags[12] = "j_knee_ri";
	level.playerTags[13] = "j_knee_le";
	level.playerTags[14] = "j_ankle_ri";
	level.playerTags[15] = "j_ankle_le";
	level.playerTags[16] = "j_helmet";
}


onPlayerConnect(player)
{
	for(;;)
	{
		level waittill( "connected", player );	
		player thread waitForFrameThread();
	}
}

visibilityMultiplierForBone(bone)
{
	multiplier = 0;
	
	switch (bone)
	{
		case "none":
			break;
		case "j_helmet":
		case "j_head":
			multiplier = 0.0216;
			break;
		case "j_neck":
			multiplier = 0.0236;
			break;
		case "j_spineupper":
			multiplier = 0.1977;
			break;
		case "j_spinelower":
			multiplier = 0.2202;
			break;
		case "j_shoulder_ri":
		case "j_shoulder_le":
			multiplier = 0.0344;
			break;
		case "j_elbow_ri":
		case "j_elbow_le":
			multiplier = 0.03394;
			break;
		case "j_wrist_ri":
		case "j_wrist_le":
			multiplier = 0.01296;
			break;
		case "j_hip_ri":
		case "j_hip_le":
			multiplier = 0.0860;
			break;
		case "j_knee_ri":
		case "j_knee_le":
			multiplier = 0.0782;
			break;
		case "j_ankle_ri":
		case "j_ankle_le":
			multiplier = 0.0118;
			break;
		case "gun":
			break;
	}

	return multiplier;
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

runVisibilityCheck(attacker, victim)
{
	start = attacker getTagOrigin("tag_eye");
	traceVisibilityAmount = 0;

	for (i = 0; i < 17; i++)
	{	
		if (sightTracePassed(start, victim getTagOrigin(level.playerTags[i]), false, attacker))
		{
			traceVisibilityAmount += 1.0 * visibilityMultiplierForBone(level.playerTags[i]);
		}
	}
	return traceVisibilityAmount;
}

vectorScale(vector, scale)
{
	return (vector[0] * scale, vector[1] * scale, vector[2] * scale);
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

	// do the tracing stuff
	start = _attacker getTagOrigin("tag_eye");
	end = location;
	trace = bulletTrace(start, end, true, _attacker);

	playerVisibilityPercentage = runVisibilityCheck(_attacker, victim);
	logLine = "Script" + type + ";" + _attacker.guid + ";" + victim.guid + ";" + _attacker GetTagOrigin("tag_eye") + ";" + location + ";" + iDamage + ";" + sWeapon + ";" + sHitLoc + ";" + sMeansOfDeath + ";" + _attacker getPlayerAngles() + ";" + gettime() + ";" + isKillstreakKill + ";" +  _attacker playerADS() + ";" + trace["fraction"] + ";" + playerVisibilityPercentage;
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

Callback_PlayerDisconnect()
{
	level notify("disconnected", self);
	self maps\mp\gametypes\_playerlogic::Callback_PlayerDisconnect();
}