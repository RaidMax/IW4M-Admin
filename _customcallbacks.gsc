#include maps\mp\_utility;
#include maps\mp\gametypes\_hud_util;
#include common_scripts\utility;

init()
{
	SetDvarIfUninitialized("sv_customcallbacks", true);
	level waittill("prematch_over");
	level.callbackPlayerKilled = ::Callback_PlayerKilled;
}


Callback_PlayerKilled( eInflictor, attacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration )
{
	victim = self;
	if (!isDefined(attacker) || !isPlayer(attacker))
		attacker = victim;
		
	logPrint("ScriptKill;" + attacker.guid + ";" + victim.guid + ";" + attacker.origin + ";" + victim.origin + ";" + iDamage + ";" + sWeapon + ";" + sHitLoc + ";" + sMeansOfDeath + "\n");
	self maps\mp\gametypes\_damage::Callback_PlayerKilled( eInflictor, attacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration );
}