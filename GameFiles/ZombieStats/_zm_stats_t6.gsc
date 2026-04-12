#include maps\mp\_utility;
#include common_scripts\utility;
#include maps\mp\zombies\_zm_utility;

// ─────────────────────────────────────────────────────────────────
// T6 Zombie Stats — Game Log Event Emitter
// ─────────────────────────────────────────────────────────────────
//
// This script is the T6 (Black Ops 2) port of _zm_stats_t4.gsc.
// While the T4 version hooks callbacks once during init and they
// remain stable for the lifetime of the match, T6 maps frequently
// override level.callback* variables AFTER init completes:
//
//   - Transit (zm_transit): bussetup() replaces callbackactordamage
//     with transit_actor_damage_override_wrapper for bus zombie
//     death animations. Affects Transit and all offshoots (Town,
//     Diner, Farm, Bus Depot).
//
//   - Origins (zm_tomb): replaces callbackactordamage with
//     tomb_actor_damage_override_wrapper for capture zone and
//     tank zombie handling.
//
//   - Die Rise (zm_highrise): achievement system replaces
//     level.perk_bought_func with its own tracker, silently
//     discarding any prior hook.
//
// Because of this, the T4 approach of "hook once, trust it stays"
// is unreliable on T6. This script uses two mitigations:
//
//   1. WATCHDOG THREAD: Periodically checks if our callback hooks
//      have been overwritten. If so, captures the new map-specific
//      function as the "original" to chain through, and re-installs
//      our hook on top. This is agnostic — works with any map
//      (including custom maps) that overrides callbacks post-init.
//
//   2. PLAYER NOTIFY LISTENERS: For perks, instead of hooking
//      level.perk_bought_func (which maps can replace), we listen
//      for the "perk_bought" notify that _zm_perks::give_perk()
//      fires on the player entity. This fires for every perk
//      acquisition on every map regardless of func overrides.
//
// ─────────────────────────────────────────────────────────────────

init()
{
    thread WaitForRoundChange();
    thread WaitForPlayerConnect();
    thread WaitForPowerupSpawned();

    // --- Zombie Round Stats (Game Log Events) --- //
    // AK  = Actor Killed        (zombie killed by player)
    // AD  = Actor Damaged       (zombie damaged by player, non-lethal)
    // K   = Kill                (player death, zombie/bleedout)
    // D   = Damage              (player damaged by zombie)
    // PD  = Player Downed       (player entered last stand)
    // PR  = Player Revived      (downed player revived)
    // PC  = Perk Consumed       (player purchased a perk)
    // PG  = Powerup Grabbed     (player picked up a powerup)
    // SU  = Stat Update         (generic stat increment)
    // RD  = Round Data          (per-player score snapshot)
    // RC  = Round Completed     (round number milestone)

    SetupCallbacks();
}

SetupCallbacks()
{
    waittillframeend;

    // zombie damage events
    level.callbackActorDamageOriginal = level.callbackactordamage;
    level.callbackActorKilledOriginal = level.callbackactorkilled;
    level.callbackactordamage = ::OnActorDamage;
    level.callbackactorkilled = ::OnActorKilled;

    // player damage events
    level.callbackPlayerDamageOriginal = level.callbackplayerdamage;
    level.callbackplayerdamage = ::OnPlayerDamaged;

    // down/revive events
    level.callbackPlayerLastStandOriginal = level.callbackplayerlaststand;
    level.callbackplayerlaststand = ::OnPlayerDowned;

    // Some maps override level.callback* variables after init
    // (e.g. Transit's bussetup, Origins' tomb wrapper).
    // This watchdog detects when our hooks have been replaced and re-applies them,
    // capturing the new map-specific functions as the originals to chain through.
    thread WatchdogCallbacks();
}

//-----------------//
//---- Waiters ----//
//-----------------//

/////////////////////////////////////////////////////////
// Waits until a player connects and spawns the
// monitoring threads
/////////////////////////////////////////////////////////
WaitForPlayerConnect()
{
    for ( ;; )
    {
        level waittill( "connecting", player );

        // T6 passes the reviver as a parameter to the player_revived notify
        // so we no longer need weapon switch monitoring to identify the reviver
        player thread WaitForPlayerRevive();

        // zm mode does not actually kill a player after down timer expires
        // they get put into spectator without a kill callback
        player thread WaitForPlayerZombified();

        // _zm_perks::give_perk() fires "perk_bought" on the player for every
        // perk acquisition on every map. This is more reliable than
        // level.perk_bought_func which can be overridden by map-specific
        // scripts (e.g. Die Rise's achievement system)
        player thread WaitForPerkBought();
    }
}

/////////////////////////////////////////////////////////
// Waits until a downed player revive timer expires
// Prints a "Kill" event to the game log
/////////////////////////////////////////////////////////
WaitForPlayerZombified()
{
    self endon( "disconnect" );

    for ( ;; )
    {
        // zombified notify occurs when a player is moved to spectator
        // after downed timer expires
        self waittill( "zombified" );
        playerInfo = BuildPlayerInfoString( self );

        logprint( "GSE;K;" + playerInfo + ";-1;-1;axis;Zombie;default_weapon;0;MOD_MELEE;none\n");
    }
}

/////////////////////////////////////////////////////////
// Waits until a downed player is revived
// Prints a "Player Revived" event to the game log
/////////////////////////////////////////////////////////
WaitForPlayerRevive()
{
    self endon ( "disconnect" );

    for ( ;; )
    {
        // T6 always passes the reviver as a parameter to the notify
        self waittill( "player_revived", reviver );

        logprint( "GSE;PR;" + BuildPlayerInfoString( self ) + ";" + BuildPlayerInfoString( reviver ) + "\n" );
    }
}

/////////////////////////////////////////////////////////
// Waits for the "perk_bought" notify fired by
// _zm_perks::give_perk() whenever a perk is acquired.
// This works on all maps regardless of perk_bought_func
/////////////////////////////////////////////////////////
WaitForPerkBought()
{
    self endon( "disconnect" );

    for ( ;; )
    {
        self waittill( "perk_bought", perk );

        logprint( "GSE;PC;" + BuildPlayerInfoString( self ) + ";" + perk + "\n" );
        logprint( "GSE;SU;" + BuildPlayerInfoString( self ) + ";" + "perks_drank" + ";" + "+1" + "\n" );
    }
}

/////////////////////////////////////////////////////////
// Monitors all four level.callback* hooks to detect when
// a map script overwrites them post-init. Re-captures the
// new map function as the original (to chain through) and
// re-installs our hook on top. Covers callbackactordamage,
// callbackactorkilled, callbackplayerdamage, and
// callbackplayerlaststand.
/////////////////////////////////////////////////////////
WatchdogCallbacks()
{
    // give map scripts time to finish their init
    // most overrides happen during map setup within the first second
    wait ( 1 );

    for ( ;; )
    {
        if ( level.callbackactordamage != ::OnActorDamage )
        {
            level.callbackActorDamageOriginal = level.callbackactordamage;
            level.callbackactordamage = ::OnActorDamage;
        }

        if ( level.callbackactorkilled != ::OnActorKilled )
        {
            level.callbackActorKilledOriginal = level.callbackactorkilled;
            level.callbackactorkilled = ::OnActorKilled;
        }

        if ( level.callbackplayerdamage != ::OnPlayerDamaged )
        {
            level.callbackPlayerDamageOriginal = level.callbackplayerdamage;
            level.callbackplayerdamage = ::OnPlayerDamaged;
        }

        if ( level.callbackplayerlaststand != ::OnPlayerDowned )
        {
            level.callbackPlayerLastStandOriginal = level.callbackplayerlaststand;
            level.callbackplayerlaststand = ::OnPlayerDowned;
        }

        // check periodically; once stable this is essentially free
        wait ( 5 );
    }
}

/////////////////////////////////////////////////////////
// Periodically checks active script_models to see if any
// have a defined powerup_name. If so monitors for pickup
/////////////////////////////////////////////////////////
WaitForPowerupSpawned()
{
    powerupEntCount = 0;

    for ( ;; )
    {
        // the powerup ent is not named and there are
        // no events to tell us when one is spawned
        // so we need to periodically check for changes
        // and wait for a player to get in range
        // additionally, overriding the level.zombie_powerup_grab_func
        // prevents original powerup code from running
        models = GetEntArray( "script_model", "classname" );
        powerupEnts = [];

        for ( i = 0; i < models.size; i++ )
        {
            if( IsDefined( models[i].powerup_name ) && !IsDefined( models[i].isWaiting ) )
            {
                powerupEnts[powerupEnts.size] = models[i];
            }
        }

        if ( powerupEnts.size != 0 && powerupEnts.size != powerupEntCount )
        {
            // we only want to start a new thread if the size increases
            // if it's decreased that means a powerup despawned
            if ( powerupEnts.size >= powerupEntCount )
            {
                array_thread( powerupEnts, ::WaitForPowerupGrab );
            }
        }

        powerupEntCount = powerupEnts.size;

        wait ( 0.05 );
    }
}

/////////////////////////////////////////////////////////
// Waits until a player gets within proximity of a powerup
// or the powerup despawns. Write powerup to game log
/////////////////////////////////////////////////////////
WaitForPowerupGrab()
{
    self.isWaiting = true;

    self endon( "powerup_timedout" );
	self endon( "powerup_grabbed" );

    while ( IsDefined( self ) )
	{
		players = get_players();

		for ( i = 0; i < players.size; i++ )
		{
            // this is not ideal, but this is the only way
            // to properly replicate how the powerup grab
            // is determined in the original code
			if ( Distance( players[i].origin, self.origin ) < 64 )
            {
                powerup = "unknown";

                if ( IsDefined( self.powerup_name ) )
                {
                    powerup = self.powerup_name;
                }

                self.isWaiting = false;

                logprint( "GSE;PG;" + BuildPlayerInfoString( players[i] ) + ";" + powerup + "\n" );
                logprint( "GSE;SU;" + BuildPlayerInfoString( players[i] ) + ";" + powerup + "_pickedup" + ";" + "+1" + "\n" );

                return;
            }
        }

        wait ( 0.05 );
    }
}

/////////////////////////////////////////////////////////
// Waits until the game is over or new round is initalized
// Writes round data to game log
/////////////////////////////////////////////////////////
WaitForRoundChange()
{
    for ( ;; )
    {
        // intermission occurs when "game over" screen appears
        // between_round_over occurs when the next round setup has completed
        result = level waittill_any_return( "intermission", "between_round_over" );

        /#
        println( "WaitForRoundStart TRIGGERED" );
        #/

        players = get_players();

        for ( i = 0; i < players.size; i++ )
        {
            // they were downed and not revived, so we already printed the event
            if ( ( IsDefined( players[i].is_zombie ) && players[i].is_zombie ) )
            {
                continue;
            }

            // if there are no zombies alive, then the game is not over
            if ( get_current_zombie_count() == 0 )
            {
                continue;
            }

            // game is over so we print out their death
            playerInfo = BuildPlayerInfoString( players[i] );
            logprint( "GSE;K;" + playerInfo + ";-1;-1;axis;Zombie;default_weapon;0;MOD_MELEE;none\n");
        }

        // IW4MAdmin reads the game log and processes events concurrently.
        // When K (death) and RD (round data) events are emitted in the same
        // server frame, they arrive simultaneously and IW4MAdmin may process
        // the RD event's stat rollup before the K event's death increment,
        // causing Deaths to be missing from match/aggregate totals.
        // This wait ensures the K events are written to the log and processed
        // before RD/RC events arrive.
        wait ( 0.1 );

        isGameOver = IsDefined( result ) && result == "intermission";
        PrintPlayerRoundData( isGameOver );

        if ( isGameOver )
        {
            break;
        }
    }
}

//-------------------//
//---- Callbacks ----//
//-------------------//

// T6 actor damage signature: (inflictor, attacker, damage, flags, meansofdeath, weapon, vpoint, vdir, shitloc, psoffsettime, boneindex)
// T4 used (eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, iModelIndex, iTimeOffset)
OnActorDamage( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, psOffsetTime, boneIndex )
{
    if ( IsPlayer( eInflictor ) || IsPlayer( eAttacker ) || IsPlayer( self ) )
    {
        victimInfo = BuildPlayerInfoString( self );
        attackerInfo = BuildPlayerInfoString( eAttacker );

        if ( IsPlayer( eInflictor ) )
        {
            attackerInfo = BuildPlayerInfoString( eInflictor );
        }

        // we only want to log damage if they aren't going to die
        // T6/Plutonium reduces self.health before the callback fires,
        // so we check if the zombie is still alive after the hit
        if ( IsDefined( self.health ) && self.health > 0 )
        {
            logprint( "GSE;AD;" + victimInfo +  ";" + attackerInfo + ";" + sWeapon + ";" + iDamage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
        }
    }

	[[ level.callbackActorDamageOriginal ]]( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, psOffsetTime, boneIndex );
}

// T6 actor killed signature: (einflictor, attacker, idamage, smeansofdeath, sweapon, vdir, shitloc, psoffsettime)
OnActorKilled( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime )
{
    if ( IsPlayer( eInflictor ) || IsPlayer( eAttacker ) || IsPlayer( self ) )
    {
        victimInfo = BuildPlayerInfoString( self );
        attackerInfo = BuildPlayerInfoString( eAttacker );
        damage = iDamage;

        if ( IsDefined( eAttacker.maxhealth ) && eAttacker.maxhealth > 0 )
        {
            damage = min( eAttacker.health, iDamage );
        }

        if ( IsPlayer( eInflictor ) )
        {
            attackerInfo = BuildPlayerInfoString( eInflictor );
            damage = min( eInflictor.health, iDamage );
        }

        logprint( "GSE;AK;" + victimInfo + ";" + attackerInfo + ";" + sWeapon + ";" + damage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
    }

    [[ level.callbackActorKilledOriginal ]]( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime );
}

// T6 player damage signature: (einflictor, eattacker, idamage, idflags, smeansofdeath, sweapon, vpoint, vdir, shitloc, psoffsettime, boneindex)
// T4 had (iModelIndex, iTimeOffset) as last two params; T6 uses (psoffsettime, boneindex)
OnPlayerDamaged( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, psOffsetTime, boneIndex )
{
    if ( IsPlayer( eInflictor ) || IsPlayer( eAttacker ) || IsPlayer( self ) )
    {
        victimInfo = BuildPlayerInfoString( self );
        attackerInfo = BuildPlayerInfoString( eAttacker );

        if ( IsPlayer( eInflictor ) )
        {
            attackerInfo = BuildPlayerInfoString( eInflictor );
        }

        logprint( "GSE;D;" + victimInfo + ";" + attackerInfo + ";" + sWeapon + ";" + iDamage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
    }

    [[ level.callbackPlayerDamageOriginal ]]( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, psOffsetTime, boneIndex );
}

// T6 laststand signature: (einflictor, eattacker, idamage, smeansofdeath, sweapon, vdir, shitloc, psoffsettime, deathanimduration)
OnPlayerDowned( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration )
{
    // sometimes this callback can be executed multiple times while the player is still downed
    // this struct is set to undefined when they die or get revived
    if ( IsDefined( self.revivetrigger ) )
    {
        return;
    }

    logprint( "GSE;PD;" + BuildPlayerInfoString( self ) + "\n" );

    [[ level.callbackPlayerLastStandOriginal ]]( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration );
}

//-----------------//
//---- Helpers ----//
//-----------------//

PrintPlayerRoundData( isGameOver )
{
    players = get_players();
    currentRound = 1;

    for( i = 0; i < players.size; i++ )
	{
        // Skip players who never spawned (e.g. joined mid-game into spectator)
        // to avoid crediting them with starting points they never earned
        if ( IsDefined( players[i].sessionstate ) && players[i].sessionstate == "spectator" )
        {
            continue;
        }

        totalScore = 0;
        currentScore = 0;

        if ( IsDefined( level.round_number ) )
        {
            currentRound = level.round_number;
        }

        if ( IsDefined ( players[i].score_total ) )
        {
            totalScore = players[i].score_total;
        }

        if ( IsDefined ( players[i].score ) )
        {
            currentScore = players[i].score;
        }

		logprint( "GSE;RD;" + BuildPlayerInfoString( players[i] ) + ";" + totalScore + ";" + currentScore + ";" + currentRound + ";" + isGameOver + "\n" );
    }

    // Ensure all RD events are processed before RC triggers StartNextRound
    // which clears round states. Without this wait, RC can race ahead of
    // late-arriving RD events due to IW4MAdmin's concurrent event processing.
    wait ( 0.1 );

    logprint( "GSE;RC;" + currentRound + "\n" );
}

BuildPlayerInfoString( entity )
{
    if ( IsPlayer( entity ) )
    {
        guid = entity getGuid();
        clientNumber = entity getEntityNumber();
        team = entity.team;
        name = entity.name;

        if ( !IsDefined( name ) )
        {
            name = "null";
        }

        return guid + ";" + clientNumber + ";" + team + ";" + name;
    }

    return "-1;-1;axis;Zombie";
}
