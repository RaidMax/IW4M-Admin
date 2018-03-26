#include maps\mp\_utility;
#include maps\mp\gametypes\_hud_util;
#include common_scripts\utility;

init()
{
	SetDvarIfUninitialized("sv_customcallbacks", true);
	level waittill("prematch_over");
	level.callbackPlayerKilled = ::Callback_PlayerKilled;
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

VectorScale(vector)
{
	vector[0] = vector[0]*100000;
	vector[1] = vector[1]*100000;
	vector[2] = vector[2]*100000;
	return vector;
}

Callback_PlayerKilled( eInflictor, attacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration )
{
	victim = self;
	_attacker = attacker;
	if (!isPlayer(attacker) && isDefined(attacker.owner))
		_attacker = attacker.owner;
	else if(!isPlayer(attacker) && sMeansOfDeath == "MOD_FALLING")
		_attacker = victim;

	
	location = victim GetTagOrigin(hitLocationToBone(sHitLoc));
	isKillstreakKill = !isPlayer(attacker) || isKillstreakWeapon(sWeapon);

	/*BulletTrace(_attacker GetTagOrigin("tag_eye"), VectorScale(anglesToForward(attacker getPlayerAngles())), true, attacker)["entity"]*/
	logPrint("ScriptKill;" + _attacker.guid + ";" + victim.guid + ";" + _attacker GetTagOrigin("tag_eye") + ";" + location + ";" + iDamage + ";" + sWeapon + ";" + sHitLoc + ";" + sMeansOfDeath + ";" + _attacker getPlayerAngles() + ";" + gettime() + ";" + isKillstreakKill + ";" +  _attacker playerADS() +  "\n");
	self maps\mp\gametypes\_damage::Callback_PlayerKilled( eInflictor, attacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration );
}