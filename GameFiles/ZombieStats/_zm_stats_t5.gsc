#include maps\_utility;
#include common_scripts\utility;
#include maps\_zombiemode_utility;

// ─────────────────────────────────────────────────────────────────
// T5 Zombie Stats — Game Log Event Emitter
// ─────────────────────────────────────────────────────────────────
//
// This script is the T5 (Black Ops 1) port of _zm_stats_t4.gsc.
// T5 shares T4's CamelCase callback naming and stable callback
// chain, but has one key difference:
//
//   - Ascension and Shangri La override level.perk_bought_func
//     with ::monkey_perk_bought for monkey round perk tracking,
//     silently discarding any prior hook.
//
// To avoid this, we use the "perk_bought" player notify fired by
// _zombiemode_perks::give_perk() on every perk acquisition,
// rather than hooking level.perk_bought_func directly.
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
    // T5 uses CamelCase callback names (same as T4)
    level.callbackActorDamageOriginal = level.callbackActorDamage;
    level.callbackActorKilledOriginal = level.callbackActorKilled;
    level.callbackActorDamage = ::OnActorDamage;
    level.callbackActorKilled = ::OnActorKilled;

    // player damage events
    level.callbackPlayerDamageOriginal = level.callbackPlayerDamage;
    level.callbackPlayerDamage = ::OnPlayerDamaged;

    // down/revive events
    level.callbackPlayerLastStandOriginal = level.callbackPlayerLastStand;
    level.callbackPlayerLastStand = ::OnPlayerDowned;
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

        // T5 passes the reviver as a parameter to the player_revived notify
        // so we do not need weapon switch monitoring to identify the reviver
        player thread WaitForPlayerRevive();

        // zm mode does not actually kill a player after down timer expires
        // they get put into spectator without a kill callback
        player thread WaitForPlayerZombified();

        // _zombiemode_perks::give_perk() fires "perk_bought" on the player
        // for every perk acquisition on every map. This is more reliable than
        // level.perk_bought_func which can be overridden by map-specific
        // scripts (e.g. Ascension/Shangri La monkey round tracking)
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

        logPrint( "GSE;K;" + playerInfo + ";-1;-1;axis;Zombie;default_weapon;0;MOD_MELEE;none\n");
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
        // T5 passes the reviver as a parameter to the notify
        self waittill( "player_revived", reviver );

        logPrint( "GSE;PR;" + BuildPlayerInfoString( self ) + ";" + BuildPlayerInfoString( reviver ) + "\n" );
    }
}

/////////////////////////////////////////////////////////
// Waits for the "perk_bought" notify fired by
// _zombiemode_perks::give_perk() whenever a perk is
// acquired. This works on all maps regardless of
// level.perk_bought_func overrides.
/////////////////////////////////////////////////////////
WaitForPerkBought()
{
    self endon( "disconnect" );

    for ( ;; )
    {
        self waittill( "perk_bought", perk );

        logPrint( "GSE;PC;" + BuildPlayerInfoString( self ) + ";" + perk + "\n" );
        logPrint( "GSE;SU;" + BuildPlayerInfoString( self ) + ";" + "perks_drank" + ";" + "+1" + "\n" );
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

                logPrint( "GSE;PG;" + BuildPlayerInfoString( players[i] ) + ";" + powerup + "\n" );
                logPrint( "GSE;SU;" + BuildPlayerInfoString( players[i] ) + ";" + powerup + "_pickedup" + ";" + "+1" + "\n" );

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
            if ( get_enemy_count() == 0 )
            {
                continue;
            }

            // game is over so we print out their death
            playerInfo = BuildPlayerInfoString( players[i] );
            logPrint( "GSE;K;" + playerInfo + ";-1;-1;axis;Zombie;default_weapon;0;MOD_MELEE;none\n");
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

// T5 actor damage signature matches T4: (eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, iModelIndex, iTimeOffset)
OnActorDamage( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, iModelIndex, iTimeOffset )
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
        if ( IsDefined( self.health ) && iDamage < self.health )
        {
            logPrint( "GSE;AD;" + victimInfo +  ";" + attackerInfo + ";" + sWeapon + ";" + iDamage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
        }
    }

	[[ level.callbackActorDamageOriginal ]]( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, iModelIndex, iTimeOffset );
}

// T5 actor killed signature matches T4: (eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, iTimeOffset)
OnActorKilled( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, iTimeOffset )
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

        logPrint( "GSE;AK;" + victimInfo + ";" + attackerInfo + ";" + sWeapon + ";" + damage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
    }

    [[ level.callbackActorKilledOriginal ]]( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, iTimeOffset );
}

// T5 player damage signature matches T4: (eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, iModelIndex, timeOffset)
OnPlayerDamaged( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, iModelIndex, timeOffset )
{
    if ( IsPlayer( eInflictor ) || IsPlayer( eAttacker ) || IsPlayer( self ) )
    {
        victimInfo = BuildPlayerInfoString( self );
        attackerInfo = BuildPlayerInfoString( eAttacker );

        if ( IsPlayer( eInflictor ) )
        {
            attackerInfo = BuildPlayerInfoString( eInflictor );
        }

        logPrint( "GSE;D;" + victimInfo + ";" + attackerInfo + ";" + sWeapon + ";" + iDamage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
    }

    [[ level.callbackPlayerDamageOriginal ]]( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, iModelIndex, timeOffset );
}

// T5 laststand signature matches T4: (eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration)
OnPlayerDowned( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration )
{
    // sometimes this callback can be executed multiple times while the player is still downed
    // this struct is set to undefined when they die or get revived
    if ( IsDefined( self.revivetrigger ) )
    {
        return;
    }

    logPrint( "GSE;PD;" + BuildPlayerInfoString( self ) + "\n" );

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

		logPrint( "GSE;RD;" + BuildPlayerInfoString( players[i] ) + ";" + totalScore + ";" + currentScore + ";" + currentRound + ";" + isGameOver + "\n" );
    }

    // Ensure all RD events are processed before RC triggers StartNextRound
    // which clears round states. Without this wait, RC can race ahead of
    // late-arriving RD events due to IW4MAdmin's concurrent event processing.
    wait ( 0.1 );

    logPrint( "GSE;RC;" + currentRound + "\n" );
}

BuildPlayerInfoString( entity )
{
    if ( IsPlayer( entity ) )
    {
        guid = entity getGuid();
        clientNumber = entity getEntityNumber();
        team = entity.team;
        name = entity.playername;

        if ( !IsDefined( name ) )
        {
            name = "null";
        }

        return guid + ";" + clientNumber + ";" + team + ";" + name;
    }

    return "-1;-1;axis;Zombie";
}
