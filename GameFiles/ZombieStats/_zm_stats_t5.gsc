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
    // Seed the bootstrap dvar so IW4MAdmin can recover the current round
    // when it starts (or reconnects to RCon) mid-match. Updated after every
    // RC event in PrintPlayerRoundData. Defaults to round 1 here so a
    // bootstrap during the very first round still resolves correctly.
    setdvar( "sv_iw4m_zm_round", 1 );

    // Stable per-match ID so IW4MAdmin can stitch a restarted/reconnected
    // process back onto the existing EFZombieMatch row instead of creating
    // a new orphaned match. Two randomints give ~10^12 collision space —
    // overkill for the "at most a few open matches per server" lookup.
    // gettime() returns 0 at init time (engine clock not yet running), so
    // we don't use it here. The lookup index is (ServerId, GameMatchId)
    // so cross-server collisions are harmless either way.
    // Set once per init (= once per map load).
    setdvar( "sv_iw4m_zm_matchid", "" + randomint( 1000000 ) + "_" + randomint( 1000000 ) );

    thread WaitForRoundChange();
    thread WaitForPlayerConnect();
    thread WaitForPowerupSpawned();
    thread WaitForWeaponPurchases();
    thread WaitForDoorPurchases();
    // T5 box detection — Kino-style chests + Der-Riese-derived custom
    // maps. Notify-driven (randomization_done / box_spin_done on
    // chest_origin) with 3-tier user resolution + scoped teddy
    // suppression. See header comment above WaitForMysteryBox.
    thread WaitForMysteryBox();
    thread WaitForBoxTeddySuppression();
    thread WaitForPackAPunch();
    thread WaitForTrapActivations();
    thread WaitForAutoTurrets();
    thread WaitForEasterEggComplete();
    thread WaitForEasterEggSteps();

    // --- Zombie Event Log Format --- //
    // Combat events (legacy format): AK, AD, K, D, RD, RC
    // Unified ZE format: down, revive, perk, powerup, weapon, box, door, trap

    SetupCallbacks();
    thread WatchdogCallbacks();
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

/////////////////////////////////////////////////////////
// Re-installs our combat-event hooks if a map script
// overwrites them post-init. Stock T5 maps don't do this,
// but custom maps occasionally chain or replace the
// callbacks — without this watchdog we'd silently lose
// AD/AK/D/down events for the rest of the game.
/////////////////////////////////////////////////////////
WatchdogCallbacks()
{
    // Give map scripts time to finish their own init before we start
    // policing — most overrides happen during the first second.
    wait ( 1 );

    for ( ;; )
    {
        if ( level.callbackActorDamage != ::OnActorDamage )
        {
            level.callbackActorDamageOriginal = level.callbackActorDamage;
            level.callbackActorDamage = ::OnActorDamage;
        }

        if ( level.callbackActorKilled != ::OnActorKilled )
        {
            level.callbackActorKilledOriginal = level.callbackActorKilled;
            level.callbackActorKilled = ::OnActorKilled;
        }

        if ( level.callbackPlayerDamage != ::OnPlayerDamaged )
        {
            level.callbackPlayerDamageOriginal = level.callbackPlayerDamage;
            level.callbackPlayerDamage = ::OnPlayerDamaged;
        }

        if ( level.callbackPlayerLastStand != ::OnPlayerDowned )
        {
            level.callbackPlayerLastStandOriginal = level.callbackPlayerLastStand;
            level.callbackPlayerLastStand = ::OnPlayerDowned;
        }

        // Once stable this is essentially free — five-second polling
        // is fine because callback overrides are init-time events.
        wait ( 5 );
    }
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
        // PaP moved to trigger-based polling in init() — see WatchPackAPunch

        ///#
        // todo: remove — debug: give max points for testing
        // Must wait for spawned_player or game overwrites score with default
        //player thread DebugGiveScore();
        //#/
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

        logPrint( "GSE;ZE;" + BuildPlayerInfoString( self ) + ";revive;" + BuildPlayerInfoString( reviver ) + "\n" );
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

        logPrint( "GSE;ZE;" + BuildPlayerInfoString( self ) + ";perk;buy;" + perk + ";0\n" );
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

                logPrint( "GSE;ZE;" + BuildPlayerInfoString( players[i] ) + ";powerup;grab;" + powerup + "\n" );

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
            // Cap reported damage at the victim's max HP — the engine can pass
            // iDamage values far in excess of what the zombie could actually absorb
            // (splash / environmental damage at high rounds).
            reportedDamage = iDamage;
            if ( IsDefined( self.maxhealth ) && self.maxhealth > 0 && reportedDamage > self.maxhealth )
            {
                reportedDamage = self.maxhealth;
            }

            logPrint( "GSE;AD;" + victimInfo +  ";" + attackerInfo + ";" + sWeapon + ";" + reportedDamage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
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

        if ( IsPlayer( eInflictor ) )
        {
            attackerInfo = BuildPlayerInfoString( eInflictor );
        }

        // Cap kill damage at the victim's max HP so the final blow doesn't
        // include overkill / engine-inflated iDamage.
        damage = iDamage;
        if ( IsDefined( self.maxhealth ) && self.maxhealth > 0 && damage > self.maxhealth )
        {
            damage = self.maxhealth;
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

    logPrint( "GSE;ZE;" + BuildPlayerInfoString( self ) + ";down\n" );

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

    setdvar( "sv_iw4m_zm_round", currentRound );
    logPrint( "GSE;RC;" + currentRound + "\n" );
}

/////////////////////////////////////////////////////////
// Economy event hooks — wall buys, box, PaP, doors, traps
// T5 uses entity-based trigger listeners (no level notifies
// for weapon_bought like T6)
/////////////////////////////////////////////////////////

/////////////////////////////////////////////////////////
// T5 Pack-a-Punch — DEBUG INSTRUMENTED BUILD
//
// Mirrors the T4 redesign (notify-driven, lock-first buyer attribution,
// distinct take/timeout outcomes via pap_taken/pap_timeout notifies).
// See _zm_stats_t4.gsc for the design rationale and engine flow notes.
//
// T5 engine reference (`_zombiemode_perks.gsc::vending_weapon_upgrade`):
//   - Trigger targetname: "zombie_vending_upgrade" (same as T4)
//   - Field: self.current_weapon (set after engine accepts buy)
//   - Take notify: self notify("pap_taken")  ~L562
//   - Timeout notify: self notify("pap_timeout")  ~L601
//   - Cost: 5000 hardcoded
//   - Timeout: level.packapunch_timeout = 15s
//
// Same flow as T4 — direct port of the design.
/////////////////////////////////////////////////////////
WaitForPackAPunch()
{
    wait ( 2 );

    triggers = getEntArray( "zombie_vending_upgrade", "targetname" );

    if ( !IsDefined( triggers ) || triggers.size == 0 )
    {
        return;
    }

    for ( i = 0; i < triggers.size; i++ )
    {
        triggers[i].iw4m_pap_buyer = undefined;
        triggers[i].iw4m_pap_buyer_weapon = undefined;
        triggers[i].iw4m_pap_taken_flag = false;
        triggers[i].iw4m_pap_timeout_flag = false;

        triggers[i] thread WatchPapOutcome();
        triggers[i] thread WatchPapTriggerForBuyer();
        triggers[i] thread WatchPapTakenFlag();
        triggers[i] thread WatchPapTimeoutFlag();
    }
}

// First-notify-wins. Engine has stale per-iter threads (wait_for_player_to_take,
// wait_for_timeout) that can outlive their iter and fire pap_taken/pap_timeout
// AFTER another notify has already resolved the iter. Ignoring later notifies
// prevents misclassification (e.g., abandon emitted as upgrade when a stale
// take notify fires after timeout cleared the iter).
WatchPapTakenFlag()
{
    for ( ;; )
    {
        self waittill( "pap_taken" );
        if ( self.iw4m_pap_taken_flag || self.iw4m_pap_timeout_flag )
        {
            continue;
        }
        self.iw4m_pap_taken_flag = true;
    }
}

WatchPapTimeoutFlag()
{
    for ( ;; )
    {
        self waittill( "pap_timeout" );
        if ( self.iw4m_pap_taken_flag || self.iw4m_pap_timeout_flag )
        {
            continue;
        }
        self.iw4m_pap_timeout_flag = true;
    }
}

WatchPapTriggerForBuyer()
{
    for ( ;; )
    {
        self waittill( "trigger", who );

        if ( IsDefined( self.iw4m_pap_buyer ) )
        {
            continue;
        }
        if ( !IsDefined( who ) || !IsPlayer( who ) )
        {
            continue;
        }

        // Phase2 path: engine already accepted a buy (current_weapon set).
        // The buyer is the player whose weapon engine just took — their
        // GetCurrentWeapon() is now empty/none. Late F-pressers in phase2
        // still hold their own weapon, so this discriminates cleanly.
        // Lock immediately with engine's current_weapon as authority; skip
        // verify (engine already accepted). Without this path, scheduler
        // ordering that runs the engine handler before ours causes legit
        // buyers to be rejected as phase2 late-pressers, dropping the emit.
        if ( IsDefined( self.current_weapon ) && self.current_weapon != "" )
        {
            buyerWeapon = who GetCurrentWeapon();
            if ( IsDefined( buyerWeapon ) && buyerWeapon != "" && buyerWeapon != "none" )
            {
                continue;
            }
            self.iw4m_pap_buyer = who;
            self.iw4m_pap_buyer_weapon = self.current_weapon;
            continue;
        }

        // Phase1 path: engine hasn't accepted yet. Replicate engine gates
        // (score, upgradeable weapon) so we don't lock on rejected presses.
        if ( !IsDefined( who.score ) || who.score < 5000 )
        {
            continue;
        }

        // T5 weapons have _zm suffix; upgrade variant is at
        // level.zombie_weapons[weapon].upgrade_name (NOT weapon + "_upgraded"
        // — that produces "weapon_zm_upgraded" vs real "weapon_upgraded_zm").
        weapon = who GetCurrentWeapon();
        if ( weapon == "" || weapon == "none" )
        {
            continue;
        }
        if ( !IsDefined( level.zombie_weapons ) || !IsDefined( level.zombie_weapons[weapon] ) )
        {
            continue;
        }
        if ( !IsDefined( level.zombie_weapons[weapon].upgrade_name ) )
        {
            continue;
        }

        self.iw4m_pap_buyer = who;
        self.iw4m_pap_buyer_weapon = weapon;

        // Verify engine actually accepted; unlock otherwise. Handles engine-side
        // gates we don't replicate (laststand, throwing grenade, switching).
        self thread VerifyPapBuyerLock();
    }
}

VerifyPapBuyerLock()
{
    // Poll for engine acceptance up to 2s. Single 0.25s wait was too short on
    // T6 (engine sometimes delays setting self.current_weapon past 0.25s,
    // unlocking legit buyer → later stale F-press re-locks wrong weapon →
    // false mismatch → skipped emit). Polling fix is identical across
    // T4/T5/T6 even though only T6 was observed failing — defensive
    // consistency.
    timeoutMs = 2000;
    pollMs = 50;
    elapsedMs = 0;
    while ( elapsedMs < timeoutMs )
    {
        if ( IsDefined( self.current_weapon ) && self.current_weapon != "" )
        {
            return;
        }
        wait ( 0.05 );
        elapsedMs = elapsedMs + pollMs;
    }

    if ( IsDefined( self.iw4m_pap_buyer ) )
    {
        self.iw4m_pap_buyer = undefined;
        self.iw4m_pap_buyer_weapon = undefined;
    }
}

WatchPapOutcome()
{
    for ( ;; )
    {
        // Reset per-iter state
        self.iw4m_pap_taken_flag = false;
        self.iw4m_pap_timeout_flag = false;
        self.iw4m_pap_buyer = undefined;
        self.iw4m_pap_buyer_weapon = undefined;

        // Phase 1: wait for engine to accept a buy (current_weapon set).
        while ( !IsDefined( self.current_weapon ) || self.current_weapon == "" )
        {
            wait ( 0.05 );
        }

        oldWeapon = self.current_weapon;

        // Phase 2: wait for iter boundary. Boundary = current_weapon changes
        // (clears OR engine immediately starts a new iter with a different
        // weapon in the same frame our poll would otherwise miss). Detecting
        // weapon-change as an exit prevents losing back-to-back iters when
        // engine clears + re-sets within one 50ms poll window.
        while ( IsDefined( self.current_weapon ) && self.current_weapon == oldWeapon )
        {
            wait ( 0.05 );
        }

        // Resolve outcome from notify flags.
        isTaken = self.iw4m_pap_taken_flag;
        isTimeout = self.iw4m_pap_timeout_flag;

        // Emit policy: engine_weapon (oldWeapon) is authoritative for what got
        // upgraded/abandoned. Locked buyer is best-effort attribution. Lock-vs-
        // engine weapon mismatch happens when player switches weapons between
        // F-presses or when scheduler ordering causes our lock to fire after
        // engine commits — engine's choice wins. Skip emission only when no
        // buyer was ever locked.
        if ( !IsDefined( self.iw4m_pap_buyer ) || !IsPlayer( self.iw4m_pap_buyer ) )
        {
            continue;
        }

        // Resolve actual cost engine charged. T5 engine sets self.cost
        // on the trigger (5000 base, 1000 during bonfire sale). T5 has no
        // attachment_cost mechanic (no re-PaP).
        cost = 5000;
        if ( IsDefined( self.cost ) ) { cost = self.cost; }

        if ( isTaken )
        {
            newWeapon = oldWeapon + "_upgraded";
            if ( IsDefined( level.zombie_weapons ) && IsDefined( level.zombie_weapons[oldWeapon] ) && IsDefined( level.zombie_weapons[oldWeapon].upgrade_name ) )
            {
                newWeapon = level.zombie_weapons[oldWeapon].upgrade_name;
            }
            logPrint( "GSE;ZE;" + BuildPlayerInfoString( self.iw4m_pap_buyer ) + ";weapon;upgrade;" + oldWeapon + ";" + newWeapon + ";" + cost + "\n" );
        }
        else if ( isTimeout )
        {
            logPrint( "GSE;ZE;" + BuildPlayerInfoString( self.iw4m_pap_buyer ) + ";weapon;abandon;" + oldWeapon + ";" + cost + "\n" );
        }
    }
}

WaitForWeaponPurchases()
{
    wait ( 2 );

    triggers = getEntArray( "weapon_upgrade", "targetname" );

    for ( i = 0; i < triggers.size; i++ )
    {
        triggers[i] thread WatchWeaponPurchase();
    }
}

WatchWeaponPurchase()
{
    for ( ;; )
    {
        self waittill( "trigger", player );

        if ( !IsDefined( player ) || !IsPlayer( player ) )
        {
            continue;
        }

        if ( !IsDefined( self.zombie_weapon_upgrade ) )
        {
            continue;
        }

        weaponName = self.zombie_weapon_upgrade;

        // Cost stored in level.zombie_weapons table, with zombie_cost as fallback
        cost = 0;
        if ( IsDefined( level.zombie_weapons ) )
        {
            if ( IsDefined( level.zombie_weapons[weaponName] ) )
            {
                if ( IsDefined( level.zombie_weapons[weaponName].cost ) )
                {
                    cost = level.zombie_weapons[weaponName].cost;
                }
            }
        }
        else if ( IsDefined( self.zombie_cost ) )
        {
            cost = self.zombie_cost;
        }

        // Check player can afford it — trigger fires on any interaction
        if ( IsDefined( player.score ) )
        {
            if ( player.score < cost )
            {
                continue;
            }
        }

        logPrint( "GSE;ZE;" + BuildPlayerInfoString( player ) + ";weapon;buy;" + weaponName + ";" + cost + "\n" );
    }
}

WaitForDoorPurchases()
{
    wait ( 2 );

    doors = getEntArray( "zombie_door", "targetname" );
    debris = getEntArray( "zombie_debris", "targetname" );

    for ( i = 0; i < doors.size; i++ )
    {
        doors[i] thread WatchDoorPurchase();
    }

    for ( i = 0; i < debris.size; i++ )
    {
        debris[i] thread WatchDoorPurchase();
    }
}

// Door triggers fire on ANY interaction, even if the player can't afford it.
// Check score before logging to avoid false positives.
WatchDoorPurchase()
{
    cost = 1000;
    if ( IsDefined( self.zombie_cost ) )
    {
        cost = self.zombie_cost;
    }

    self waittill( "trigger", player );

    if ( !IsDefined( player ) || !IsPlayer( player ) )
    {
        return;
    }

    if ( !IsDefined( player.score ) || player.score < cost )
    {
        return;
    }

    logPrint( "GSE;ZE;" + BuildPlayerInfoString( player ) + ";door;buy;" + cost + "\n" );
}

/////////////////////////////////////////////////////////
// T5 Mystery Box Detection (Kino-style chests + Der-Riese-derived
// custom maps).
//
// Engine reference: `_zombiemode_weapons.gsc` from the T5 ZM scripts.
// Key entities/state:
//   - Chest trigger: targetname "treasure_chest_use"
//   - self.chest_origin: pre-cached on the chest at init (L948 ref).
//     Holds .weapon_string which cycles during randomization, then is
//     cleared by the engine at L2227.
//   - self.chest_user: assigned BEFORE randomization on T5 (L1124/1131,
//     unlike T4 which sets it AFTER), cleared at L1296. The pre-anim
//     assignment gives a wider window than T4 — chest_user is reliably
//     observable at randomization_done time on the normal grab path.
//   - self.timedOut: false at top of every iter (L1156), true on 12s
//     no-grab timeout (L1242). Used as the per-chest "is Der-Riese-
//     -style chest" discriminator (no map-name check) for tier-3
//     resolution gating.
//   - chest_origin "randomization_done" notify (L2145): emitted once per
//     iter after the spin completes, with weapon_string already final.
//     We use this as the iter-start signal.
//   - chest_origin "box_spin_done" notify (L2228): emitted at the end of
//     treasure_chest_weapon_spawn after grab/timeout/teddy. Used as the
//     iter-end signal. self.timedOut is safe to read here because the
//     engine then does `wait 3` at L1286 before resetting it.
//   - level "weapon_fly_away_start" notify (L2150): fires on the teddy
//     path ~0.5s after randomization_done.
//
// Why notify-driven instead of polling: the original polling design
// conflated multiple engine box iterations into one detection during
// F-spam (chest_user transitioned undefined→defined inside one poll
// interval). Each engine pull emits exactly one randomization_done +
// one box_spin_done, so notify boundaries give 1:1 mapping.
//
// 3-tier user resolution (same shape as T4):
//   1. capturedUser snapshotted at randomization_done resume
//   2. live self.chest_user at box_spin_done resume
//   3. self.iw4m_box_last_trigger (parallel waittill ground truth)
//      — gated on IsDefined(self.timedOut)
// Tier 3 is what catches teddy attribution: the engine drops from L2145
// → L1296 in one frame on the teddy path (no yield), so chest_user has
// already been cleared by the time we resume — both tier 1 and tier 2
// miss. The trigger waittill captured the buyer earlier.
//
// Teddy bears: emit `box;teddy;{cost}` matching T6's shape. weapon_string
// is set undefined by the engine at L2123 before randomization_done, so
// no weapon name is reported.
//
// Per-chest state:
//   - self.iw4m_box_teddy_marker    — set by suppression, consumed at
//     box_spin_done
//   - self.iw4m_box_in_late_phase   — true between rand_done and iter
//     end; suppression only marks chests with this flag
//   - self.iw4m_box_last_trigger    — parallel trigger capture, cleared
//     at iter end
//
// T5 GSC quirk: do NOT rely on short-circuit `IsDefined(x) && x` —
// nested ifs throughout.
/////////////////////////////////////////////////////////
WaitForMysteryBox()
{
    wait ( 5 );

    chests = getEntArray( "treasure_chest_use", "targetname" );

    if ( !IsDefined( chests ) )
    {
        return;
    }

    if ( chests.size == 0 )
    {
        return;
    }

    for ( i = 0; i < chests.size; i++ )
    {
        chests[i].iw4m_box_teddy_marker = false;
        chests[i].iw4m_box_in_late_phase = false;

        chests[i] thread WatchBoxOutcome();
        chests[i] thread WatchBoxTriggerForBuyer();
    }
}

/////////////////////////////////////////////////////////
// Teddy bear suppression — same scoping rule as T4: only mark chests
// flagged as iw4m_box_in_late_phase, otherwise level-scoped marking
// bleeds across iterations.
/////////////////////////////////////////////////////////
WaitForBoxTeddySuppression()
{
    wait ( 5 );
    for ( ;; )
    {
        level waittill( "weapon_fly_away_start" );

        chests = getEntArray( "treasure_chest_use", "targetname" );
        if ( !IsDefined( chests ) )
        {
            continue;
        }
        if ( chests.size == 0 )
        {
            continue;
        }

        for ( k = 0; k < chests.size; k++ )
        {
            if ( IsDefined( chests[k].iw4m_box_in_late_phase ) )
            {
                if ( chests[k].iw4m_box_in_late_phase )
                {
                    chests[k].iw4m_box_teddy_marker = true;
                }
            }
        }
    }
}

/////////////////////////////////////////////////////////
// Parallel ground-truth capture of who pressed USE on the chest.
//
// Multiple GSC threads can waittill the same notify and all resume,
// so this peacefully co-exists with treasure_chest_think's two
// waittills (BUY at L1080, GRAB at L1201).
//
// Lock-on-first-valid-press: record only the FIRST trigger of an
// iteration that comes from a player who could afford the buy, then
// ignore every subsequent press until iter end clears the lock. This
// is the buyer because the engine enforces buyer-only-can-grab
// (treasure_chest_think rejects "trigger" from non-buyers). The
// buyer's own GRAB press fires trigger again but they're already
// locked so it's a noop. Without the lock, in multi-player F-spam,
// another player's noise press would overwrite the real buyer.
//
// Affordability gate: replicates the engine's own score check so a
// player pressing F with insufficient funds doesn't lock attribution.
/////////////////////////////////////////////////////////
WatchBoxTriggerForBuyer()
{
    for ( ;; )
    {
        self waittill( "trigger", who );

        if ( IsDefined( self.iw4m_box_last_trigger ) )
        {
            continue;
        }
        if ( !IsDefined( who ) || !IsPlayer( who ) )
        {
            continue;
        }

        cost = 950;
        if ( IsDefined( level.zombie_treasure_chest_cost ) )
        {
            cost = level.zombie_treasure_chest_cost;
        }
        else if ( IsDefined( self.zombie_cost ) )
        {
            cost = self.zombie_cost;
        }

        if ( !IsDefined( who.score ) || who.score < cost )
        {
            continue;
        }

        self.iw4m_box_last_trigger = who;
    }
}

WatchBoxOutcome()
{
    if ( !IsDefined( self.chest_origin ) )
    {
        return;
    }

    for ( ;; )
    {
        // Iter starts when the engine signals randomization is done.
        self.chest_origin waittill( "randomization_done" );

        // Mark for teddy suppression scoping. Cleared at iter end.
        self.iw4m_box_in_late_phase = true;

        // Snapshot weapon + buyer immediately. weapon_string is undefined
        // on the teddy path (engine clears it at L2123 BEFORE rand_done).
        // chest_user may also be undefined on teddy because the engine
        // drops L2145 → L1296 in one frame without yielding — that's why
        // tier-3 trigger fallback is required for teddy attribution.
        weaponName = "undef";
        if ( IsDefined( self.chest_origin.weapon_string ) )
        {
            weaponName = self.chest_origin.weapon_string;
        }

        capturedUser = undefined;
        if ( IsDefined( self.chest_user ) )
        {
            if ( IsPlayer( self.chest_user ) )
            {
                capturedUser = self.chest_user;
            }
        }

        // Wait for outcome.
        self.chest_origin waittill( "box_spin_done" );

        // Safe to read final state — engine sleeps `wait 3` at L1286
        // before resetting timedOut for the next iter.
        timedOutDefined = 0;
        timedOutValue = false;
        if ( IsDefined( self.timedOut ) )
        {
            timedOutDefined = 1;
            if ( self.timedOut )
            {
                timedOutValue = true;
            }
        }

        isTeddy = false;
        if ( IsDefined( self.iw4m_box_teddy_marker ) )
        {
            if ( self.iw4m_box_teddy_marker )
            {
                isTeddy = true;
            }
        }
        self.iw4m_box_teddy_marker = false;

        // 3-tier user resolution.
        user = capturedUser;
        if ( !IsDefined( user ) )
        {
            if ( IsDefined( self.chest_user ) )
            {
                if ( IsPlayer( self.chest_user ) )
                {
                    user = self.chest_user;
                }
            }
        }
        if ( !IsDefined( user ) )
        {
            if ( timedOutDefined == 1 )
            {
                if ( IsDefined( self.iw4m_box_last_trigger ) )
                {
                    if ( IsPlayer( self.iw4m_box_last_trigger ) )
                    {
                        user = self.iw4m_box_last_trigger;
                    }
                }
            }
        }

        cost = 950;
        if ( IsDefined( level.zombie_treasure_chest_cost ) )
        {
            cost = level.zombie_treasure_chest_cost;
        }
        else if ( IsDefined( self.zombie_cost ) )
        {
            cost = self.zombie_cost;
        }

        if ( IsDefined( user ) )
        {
            if ( isTeddy )
            {
                logPrint( "GSE;ZE;" + BuildPlayerInfoString( user ) + ";box;teddy;" + cost + "\n" );
            }
            else if ( timedOutValue )
            {
                logPrint( "GSE;ZE;" + BuildPlayerInfoString( user ) + ";box;pass;" + weaponName + ";" + cost + "\n" );
            }
            else
            {
                logPrint( "GSE;ZE;" + BuildPlayerInfoString( user ) + ";box;take;" + weaponName + ";" + cost + "\n" );
            }
        }

        // Per-iter cleanup.
        self.iw4m_box_last_trigger = undefined;
        self.iw4m_box_in_late_phase = false;
    }
}

/////////////////////////////////////////////////////////
// T5 Trap Detection — POLLING _trap_in_use
//
// T5's trap system only fires "trap_activate" notify from
// trap_activate_electric(). Fire, rotating, and flipper traps
// do NOT fire this notify — they just run their activate function.
// Auto turrets (Kino) use a completely separate system.
//
// Solution: poll trap._trap_in_use instead. This flag is set to 1
// by the game's trap_think() (line 317 ref) when ANY trap type is
// purchased, regardless of type. It's set to 0 on cooldown.
/////////////////////////////////////////////////////////
WaitForTrapActivations()
{
    wait ( 2 );

    traps = getEntArray( "zombie_trap", "targetname" );

    for ( i = 0; i < traps.size; i++ )
    {
        traps[i] thread WatchTrapActivation();
    }

    // Also scan for non-standard trap triggers (custom maps)
    allTriggers = getEntArray( "trigger_use", "classname" );

    for ( i = 0; i < allTriggers.size; i++ )
    {
        if ( !IsDefined( allTriggers[i].targetname ) )
        {
            continue;
        }

        name = allTriggers[i].targetname;

        if ( name == "zombie_trap" )
        {
            continue;
        }

        if ( IsSubStr( name, "trap" ) )
        {
            allTriggers[i] thread WatchTrapActivation();
        }
    }
}

// Polls _trap_in_use instead of waittill("trap_activate").
// _trap_in_use is set to 1 on purchase (all trap types), 0 on cooldown.
WatchTrapActivation()
{
    trapType = "trap";
    if ( IsDefined( self.script_noteworthy ) )
    {
        trapType = self.script_noteworthy;
    }

    cost = 1000;
    if ( IsDefined( self.zombie_cost ) )
    {
        cost = self.zombie_cost;
    }

    for ( ;; )
    {
        // Wait for trap to be activated (purchased)
        // T5 GSC doesn't short-circuit || — use nested ifs
        while ( true )
        {
            if ( IsDefined( self._trap_in_use ) )
            {
                if ( self._trap_in_use == 1 )
                {
                    break;
                }
            }
            wait ( 0.2 );
        }

        players = getPlayers();
        closest = undefined;
        closestDist = 99999;

        for ( i = 0; i < players.size; i++ )
        {
            if ( !IsAlive( players[i] ) )
            {
                continue;
            }

            dist = distance( players[i].origin, self.origin );
            if ( dist < closestDist )
            {
                closestDist = dist;
                closest = players[i];
            }
        }

        if ( IsDefined( closest ) )
        {
            logPrint( "GSE;ZE;" + BuildPlayerInfoString( closest ) + ";trap;activate;" + trapType + ";" + cost + "\n" );
        }

        // Wait for trap to finish and cool down (_trap_in_use goes back to 0)
        // before polling for the next activation
        while ( IsDefined( self._trap_in_use ) )
        {
            if ( self._trap_in_use != 1 )
            {
                break;
            }
            wait ( 1 );
        }
    }
}

/////////////////////////////////////////////////////////
// T5 Auto Turret Detection
//
// Used on: Kino, Ascension, Call of the Dead, Five.
// Separate system from zombie_trap — uses _zombiemode_auto_turret module.
// Entities: script_noteworthy = "auto_turret_trigger"
// State: self.turret_active (true when active, false on cooldown)
// Cost: level.auto_turret_cost (default 1500)
/////////////////////////////////////////////////////////
WaitForAutoTurrets()
{
    wait ( 2 );

    if ( !IsDefined( level.auto_turret_array ) )
    {
        return;
    }

    for ( i = 0; i < level.auto_turret_array.size; i++ )
    {
        level.auto_turret_array[i] thread WatchAutoTurret();
    }
}

WatchAutoTurret()
{
    cost = 1500;
    if ( IsDefined( level.auto_turret_cost ) )
    {
        cost = level.auto_turret_cost;
    }

    for ( ;; )
    {
        // Poll for turret_active going true (set on purchase)
        while ( true )
        {
            if ( IsDefined( self.turret_active ) )
            {
                if ( self.turret_active )
                {
                    break;
                }
            }
            wait ( 0.2 );
        }

        // Find nearest player
        players = get_players();
        closest = undefined;
        closestDist = 99999;

        for ( i = 0; i < players.size; i++ )
        {
            if ( !IsAlive( players[i] ) )
            {
                continue;
            }

            dist = distance( players[i].origin, self.origin );
            if ( dist < closestDist )
            {
                closestDist = dist;
                closest = players[i];
            }
        }

        if ( IsDefined( closest ) )
        {
            logPrint( "GSE;ZE;" + BuildPlayerInfoString( closest ) + ";trap;activate;turret;" + cost + "\n" );
        }

        // Wait for turret to deactivate before re-polling
        while ( true )
        {
            if ( IsDefined( self.turret_active ) )
            {
                if ( !self.turret_active )
                {
                    break;
                }
            }
            wait ( 1 );
        }
    }
}

//-----------------------//
//---- Utility/Infra ----//
//-----------------------//

///#
// todo: remove
DebugGiveScore()
{
    self endon( "disconnect" );
    self waittill( "spawned_player" );
    wait ( 0.5 );
    self.score = 1000000;
}
//#/

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

/////////////////////////////////////////////////////////
// Easter Egg main quest detection.
//
// Each T5 map with a "main" sidequest has a level notify fired when the
// quest reaches its terminal state. Ascension is the exception — its
// terminal is a flag (`weapons_combined`), not a notify, so its canonical
// emission lives inside HookAscensionCasimir below alongside the per-step
// flag watchers (single source of truth for the same flag).
//
// Reference: pulled from t5-scripts-main per-map *_sq.gsc / *_achievement.gsc.
// Custom maps / Five / Kino / Verruckt / Nacht / Shi No / Dead Ops have no
// main EE → silently no-op (debug log records "no watcher configured").
/////////////////////////////////////////////////////////
WaitForEasterEggComplete()
{
    level endon( "end_game" );

    notifyName = "";
    switch ( level.script )
    {
        case "zombie_coast":  notifyName = "coast_easter_egg_achieved";        break;  // Call of the Dead
        case "zombie_temple": notifyName = "temple_sidequest_achieved";        break;  // Shangri-La
        case "zombie_moon":   notifyName = "moon_sidequest_big_bang_achieved"; break;  // Moon
        // zombie_cosmodrome: terminal is a flag, handled in HookAscensionCasimir.
        default:
            logprint( "[ZM-EE] No EE watcher configured for map=" + level.script + "\n" );
            return;
    }

    logprint( "[ZM-EE] Watcher armed: map=" + level.script + " notify=" + notifyName + "\n" );

    level waittill( notifyName );

    if ( IsDefined( level.iw4m_ee_fired ) && level.iw4m_ee_fired )
    {
        logprint( "[ZM-EE] Suppressed re-emit on map=" + level.script + " (already fired)\n" );
        return;
    }
    level.iw4m_ee_fired = true;

    roundStr = "?";
    if ( IsDefined( level.round_number ) ) { roundStr = "" + level.round_number; }
    logprint( "[ZM-EE] EE complete fired for map=" + level.script + " round=" + roundStr + "\n" );
    logprint( "GSE;EE;" + level.script + "\n" );
}

/////////////////////////////////////////////////////////
// Easter Egg STEP detection — song-egg progression.
//
// T5 stock song-EE pattern is uniform across maps: N entities share a
// single targetname; each emits "trigger" on USE (press F); a stock
// per-map *_amb.gsc handler increments a level counter and fires
// change_zombie_music("egg") at counter==N. We hook the same trigger
// notify in parallel — broadcast, so we don't interfere with stock
// counter logic — and emit one step per entity.
//
//   • Kino der Toten (zombie_theater)  — `meteor_egg_trigger` × 3
//   • Five           (zombie_pentagon) — `secret_phone_trig`  × 3
//
// All paths gate on first-player-connect: dedicated servers may pause
// level-time when no players are connected, so subsequent waits behave
// normally only after someone joins.
//
// Within a map, step number reflects iteration order from getentarray
// (stable per spawn) — not a physical mapping. From a "did they hit
// all N" perspective this is irrelevant.
//
// Custom maps that don't use these entity names silently no-op.
/////////////////////////////////////////////////////////
WaitForEasterEggSteps()
{
    level endon( "end_game" );

    while ( getplayers().size == 0 )
    {
        wait ( 1 );
    }

    switch ( level.script )
    {
        case "zombie_theater":    HookT5SongTriggers( "meteor_egg_trigger", "t5_kn_meteor" ); return;
        case "zombie_pentagon":   HookT5SongTriggers( "secret_phone_trig",  "t5_fv_phone"  ); return;
        case "zombie_cosmodrome": HookAscension();                                            return;
        case "zombie_coast":      HookCallOfDead();                                           return;
        case "zombie_temple":     HookShangriLa();                                            return;
        case "zombie_moon":       HookMoon();                                                 return;
        default:
            logprint( "[ZM-EE] No step watcher configured for map=" + level.script + "\n" );
            return;
    }
}

// Generic T5 song-EE hook — N USE-triggers sharing one targetname, each
// fires "trigger" on press. stepKeyPrefix gets _<1-based index> appended.
HookT5SongTriggers( targetName, stepKeyPrefix )
{
    level endon( "end_game" );

    // Poll for the entity batch — stock _amb scripts thread their setup with
    // a leading wait, so triggers may not exist yet at our level-init time.
    // 30s ceiling is well past stock init.
    triggers = undefined;
    for ( attempt = 0; attempt < 30; attempt++ )
    {
        triggers = getentarray( targetName, "targetname" );
        if ( IsDefined( triggers ) && triggers.size > 0 )
        {
            break;
        }
        wait ( 1 );
    }

    if ( !IsDefined( triggers ) || triggers.size == 0 )
    {
        logprint( "[ZM-EE] " + level.script + ": no '" + targetName + "' entities found after polling (custom map?)\n" );
        return;
    }

    logprint( "[ZM-EE] Step watcher armed: " + stepKeyPrefix + " count=" + triggers.size + "\n" );

    for ( i = 0; i < triggers.size; i++ )
    {
        triggers[i] thread WatchT5SongTriggerStep( stepKeyPrefix, i + 1 );
    }
}

WatchT5SongTriggerStep( stepKeyPrefix, oneBasedIndex )
{
    self endon( "death" );
    level endon( "end_game" );

    stepKey = stepKeyPrefix + "_" + oneBasedIndex;

    self waittill( "trigger" );

    logprint( "[ZM-EE] Step fired: " + stepKey + "\n" );
    logprint( "GSE;EE;step;" + stepKey + "\n" );
}

/////////////////////////////////////////////////////////
// Ascension (zombie_cosmodrome) — two parallel quests:
//
//   Song EE — three teddy bears (mus_teddybear). Stock spawns a runtime
//   trigger_radius beneath each bear (player must touch + use), so we
//   can't waittill("trigger") on the bear entity directly. Instead we
//   poll level.teddybear_counter — same pattern as T4 Der Riese flytrap.
//
//   Casimir Mechanism — six named flags, set as each step completes
//   (zombie_cosmodrome_eggs.gsc::init flag_inits all six). Step 6's flag
//   `weapons_combined` is also the canonical terminal for the main EE,
//   so we emit BOTH the step event and the canonical map-level event
//   from the same watcher (with the iw4m_ee_fired guard to prevent
//   re-emit, mirroring WaitForEasterEggComplete's contract).
//
// Step 3 (`switches_synced`) is hard-gated to 4 players in stock script
// (`pressed == 4`), so the quest can't physically complete sub-4P. Premium
// config sets MinPlayers=4 on the casimir quest to hide it from leaderboard
// renders for sub-4P matches; step events still flow regardless.
/////////////////////////////////////////////////////////
HookAscension()
{
    level endon( "end_game" );

    thread HookAscensionTeddyBears();
    thread HookAscensionCasimir();
}

HookAscensionTeddyBears()
{
    level endon( "end_game" );

    logprint( "[ZM-EE] Step watcher armed: t5_as_bear (polling level.teddybear_counter)\n" );

    // Same loop shape as T4 Der Riese flytrap_counter. Engine is single-
    // threaded so a ticked-past value would be missed if we slept too long;
    // 0.5s easily covers human reaction time between bear interactions.
    prev = 0;
    while ( !IsDefined( level.teddybear_counter ) || level.teddybear_counter < 3 )
    {
        if ( IsDefined( level.teddybear_counter ) && level.teddybear_counter > prev )
        {
            for ( i = prev + 1; i <= level.teddybear_counter && i <= 3; i++ )
            {
                stepKey = "t5_as_bear_" + i;
                logprint( "[ZM-EE] Step fired: " + stepKey + "\n" );
                logprint( "GSE;EE;step;" + stepKey + "\n" );
            }
            prev = level.teddybear_counter;
        }
        wait ( 0.5 );
    }

    // 2→3 tick exits the loop early — flush any unobserved steps.
    for ( i = prev + 1; i <= 3; i++ )
    {
        stepKey = "t5_as_bear_" + i;
        logprint( "[ZM-EE] Step fired: " + stepKey + "\n" );
        logprint( "GSE;EE;step;" + stepKey + "\n" );
    }
}

HookAscensionCasimir()
{
    level endon( "end_game" );

    logprint( "[ZM-EE] Step watcher armed: t5_as_cm (6 Casimir flags)\n" );

    // Order matches stock zombie_cosmodrome_eggs.gsc::init flag_init order
    // and the community walkthrough sequence (1=teleport, 2=power, 3=switches,
    // 4=plate, 5=lander, 6=weapons). Stock physically gates step 3 to 4P; the
    // others can be solo'd in script terms but the quest can't complete without
    // step 3. Premium UI hides this quest entirely for sub-4P matches.
    thread WatchT5Flag( "target_teleported",  "t5_as_cm_1", false );
    thread WatchT5Flag( "rerouted_power",     "t5_as_cm_2", false );
    thread WatchT5Flag( "switches_synced",    "t5_as_cm_3", false );
    thread WatchT5Flag( "pressure_sustained", "t5_as_cm_4", false );
    thread WatchT5Flag( "passkey_confirmed",  "t5_as_cm_5", false );
    thread WatchT5Flag( "weapons_combined",   "t5_as_cm_6", true  );  // canonical terminal
}

WatchT5Flag( flagName, stepKey, isCanonical )
{
    level endon( "end_game" );

    // Stock flag_init runs in zombie_cosmodrome_eggs::init, but we may race
    // it depending on script load order. Poll until level.flag[<name>] exists
    // before calling flag_wait, which would otherwise deref undefined and
    // throw a runtime cast error (same gotcha as T4 Der Riese hide_and_seek).
    while ( !IsDefined( level.flag ) || !IsDefined( level.flag[ flagName ] ) )
    {
        wait ( 0.5 );
    }

    flag_wait( flagName );

    logprint( "[ZM-EE] Step fired: " + stepKey + "\n" );
    logprint( "GSE;EE;step;" + stepKey + "\n" );

    if ( !isCanonical )
    {
        return;
    }

    // Canonical terminal — also emit the map-level EE-complete event.
    if ( IsDefined( level.iw4m_ee_fired ) && level.iw4m_ee_fired )
    {
        logprint( "[ZM-EE] Suppressed re-emit on map=" + level.script + " (already fired)\n" );
        return;
    }
    level.iw4m_ee_fired = true;

    roundStr = "?";
    if ( IsDefined( level.round_number ) ) { roundStr = "" + level.round_number; }
    logprint( "[ZM-EE] EE complete fired for map=" + level.script + " round=" + roundStr + "\n" );
    logprint( "GSE;EE;" + level.script + "\n" );
}

/////////////////////////////////////////////////////////
// Call of the Dead (zombie_coast) — two parallel quests:
//
//   Song EE — three Element 115 fragments. Stock zombie_coast_amb.gsc
//   spawns runtime trigger_radius beneath each `mus_easteregg` STRUCT
//   (note: structs, not entities — getstructarray) and increments
//   level.meteor_counter on each touch+use. Same poll pattern as
//   Ascension teddy bears; can't waittill on a struct.
//
//   Ensemble Cast — 9 named flag steps, terminal `dmf` flag set
//   immediately before the existing `coast_easter_egg_achieved` notify.
//   We DO NOT mark dmf as canonical here because WaitForEasterEggComplete
//   already handles the notify — emitting the canonical from both paths
//   would double-fire. Premium config sets MinPlayers=2 on the Ensemble
//   Cast quest because:
//     • Step 3 (vodka, flag `bd`) PHYSICALLY can't fire solo — virgo
//       thread isn't spawned for `players.size <= 1` and there's no
//       side-effect path.
//     • Steps 4 (morse `aca`), 6 (foghorns `bp`), 7 (dials `ss`) DO
//       fire automatically in solo via stock script side-effects (door-
//       knock condition / metal_horse solo branch), so a solo run can
//       complete the EE — but the resulting "all coop steps done"
//       progression would advertise actions the player never performed.
//   Hide entirely on solo runs, render normally on 2P+.
/////////////////////////////////////////////////////////
HookCallOfDead()
{
    level endon( "end_game" );

    thread WatchT5MeteorCounterSong( "t5_cd_fragment" );
    thread HookCallOfDeadEnsemble();
}

HookCallOfDeadEnsemble()
{
    level endon( "end_game" );

    logprint( "[ZM-EE] Step watcher armed: t5_cd_ec (9 Ensemble Cast flags)\n" );

    // Order matches community walkthrough. Each maps to a flag set in
    // zombie_coast_eggs.gsc when that step's terminal action completes.
    // None are marked canonical — the canonical map-level event is fired
    // from WaitForEasterEggComplete on the existing `coast_easter_egg_achieved`
    // notify, which immediately follows `dmf` flag-set in stock script.
    thread WatchT5Flag( "ffd", "t5_cd_ec_1", false );  // step 2 — Fuse delivered
    thread WatchT5Flag( "hgd", "t5_cd_ec_2", false );  // step 3 — Generators destroyed (4)
    thread WatchT5Flag( "bd",  "t5_cd_ec_3", false );  // step 4 — Vodka delivered (coop-only, never solo)
    thread WatchT5Flag( "aca", "t5_cd_ec_4", false );  // step 5 — Morse code (auto-set in solo)
    thread WatchT5Flag( "shs", "t5_cd_ec_5", false );  // step 6 — Ship's bridge (wheel + levers)
    thread WatchT5Flag( "bp",  "t5_cd_ec_6", false );  // step 7 — Foghorns (auto-set in solo)
    thread WatchT5Flag( "ss",  "t5_cd_ec_7", false );  // step 8 — Tower dials (auto-set in solo)
    thread WatchT5Flag( "re",  "t5_cd_ec_8", false );  // step 9 — Sacrifice / Vrill device retrieved
    thread WatchT5Flag( "dmf", "t5_cd_ec_9", false );  // step 10 — Final knife / death-machine fire
}

/////////////////////////////////////////////////////////
// Shared helpers used by multiple T5 maps.
/////////////////////////////////////////////////////////

// Shared `level.meteor_counter` poll-to-3 song-EE watcher. Used by maps that
// follow the stock Treyarch pattern: 3 `mus_easteregg` STRUCTS, each spawning
// a runtime trigger_radius (so we can't waittill on the struct), with the
// counter incremented on each touch+use. Pattern: Coast (zombie_coast),
// Shangri-La (zombie_temple). Ascension uses level.teddybear_counter instead
// (different name) so it has its own watcher.
WatchT5MeteorCounterSong( stepKeyPrefix )
{
    level endon( "end_game" );

    logprint( "[ZM-EE] Step watcher armed: " + stepKeyPrefix + " (polling level.meteor_counter)\n" );

    // Same loop shape as HookAscensionTeddyBears / T4 Der Riese flytrap.
    // 0.5s cadence covers human reaction time between fragment interactions.
    prev = 0;
    while ( !IsDefined( level.meteor_counter ) || level.meteor_counter < 3 )
    {
        if ( IsDefined( level.meteor_counter ) && level.meteor_counter > prev )
        {
            for ( i = prev + 1; i <= level.meteor_counter && i <= 3; i++ )
            {
                stepKey = stepKeyPrefix + "_" + i;
                logprint( "[ZM-EE] Step fired: " + stepKey + "\n" );
                logprint( "GSE;EE;step;" + stepKey + "\n" );
            }
            prev = level.meteor_counter;
        }
        wait ( 0.5 );
    }

    // 2→3 tick exits early — flush remainder.
    for ( i = prev + 1; i <= 3; i++ )
    {
        stepKey = stepKeyPrefix + "_" + i;
        logprint( "[ZM-EE] Step fired: " + stepKey + "\n" );
        logprint( "GSE;EE;step;" + stepKey + "\n" );
    }
}

// Generic level-notify watcher. Used for stock notify-driven step events
// (e.g. _zombiemode_sidequests::stage_completed_internal which fires
// `<sq>_<stage>_completed`). isCanonical mirrors WatchT5Flag's contract.
WatchT5LevelNotify( notifyName, stepKey, isCanonical )
{
    level endon( "end_game" );

    level waittill( notifyName );

    logprint( "[ZM-EE] Step fired: " + stepKey + "\n" );
    logprint( "GSE;EE;step;" + stepKey + "\n" );

    if ( !isCanonical )
    {
        return;
    }

    if ( IsDefined( level.iw4m_ee_fired ) && level.iw4m_ee_fired )
    {
        logprint( "[ZM-EE] Suppressed re-emit on map=" + level.script + " (already fired)\n" );
        return;
    }
    level.iw4m_ee_fired = true;

    roundStr = "?";
    if ( IsDefined( level.round_number ) ) { roundStr = "" + level.round_number; }
    logprint( "[ZM-EE] EE complete fired for map=" + level.script + " round=" + roundStr + "\n" );
    logprint( "GSE;EE;" + level.script + "\n" );
}

/////////////////////////////////////////////////////////
// Shangri-La (zombie_temple) — two parallel quests:
//
//   Song EE — three Element 115 fragments, same poll-counter pattern as
//   Coast (level.meteor_counter). Uses shared WatchT5MeteorCounterSong.
//
//   Time Travel Will Tell — uses the stock _zombiemode_sidequests
//   framework: 8 declared stages each emitting `sq_<name>_completed`
//   level notify on completion, plus 2 inter-stage flags
//   (gongs_resonating, meteorite_shrunk) that gate stage transitions.
//   The terminal stage (BaG) completion triggers the existing
//   `temple_sidequest_achieved` notify (already wired in
//   WaitForEasterEggComplete), so none of our watchers mark canonical
//   here — emitting from both paths would double-fire.
//
// Premium config sets MinPlayers=4 because step 2 (OaFC, floor tiles) has
// its solo auto-pass branch wrapped in /# #/ debug blocks (stripped in
// Pluto T5 production builds), and step 3 (DgCWf, water slide) requires
// `level._on_plate >= 3` simultaneously with someone going down — physically
// impossible with <4 players. Steps still fire regardless; UI hides quest
// for sub-4P matches.
/////////////////////////////////////////////////////////
HookShangriLa()
{
    level endon( "end_game" );

    thread WatchT5MeteorCounterSong( "t5_sl_fragment" );
    thread HookShangriLaSidequest();
}

HookShangriLaSidequest()
{
    level endon( "end_game" );

    logprint( "[ZM-EE] Step watcher armed: t5_sl_sq (10 Time Travel Will Tell steps)\n" );

    // Stages 2-8 use the sidequest framework's <sq>_<stagename>_completed
    // notify. Stage names are the case-sensitive identifiers from
    // declare_sidequest_stage() in zombie_temple_sq_*.gsc.
    thread WatchT5LevelNotify( "sq_OaFC_completed",  "t5_sl_sq_1", false );  // step 2 — Floor Tiles
    thread WatchT5LevelNotify( "sq_DgCWf_completed", "t5_sl_sq_2", false );  // step 3 — Water Slide
    thread WatchT5LevelNotify( "sq_LGS_completed",   "t5_sl_sq_3", false );  // step 4 — Water Slide Crystal
    thread WatchT5LevelNotify( "sq_PtT_completed",   "t5_sl_sq_4", false );  // step 5 — Gas Pipes
    thread WatchT5LevelNotify( "sq_StD_completed",   "t5_sl_sq_5", false );  // step 6 — Spikemores
    thread WatchT5LevelNotify( "sq_bttp_completed",  "t5_sl_sq_6", false );  // step 7 — Wall Panels + Snare
    thread WatchT5LevelNotify( "sq_bttp2_completed", "t5_sl_sq_7", false );  // step 8 — Mud Room Dials

    // Step 9 (gongs + dynamite catch) terminates with the gongs_resonating
    // flag set, NOT a sidequest stage — gongs are handled by gong_watcher
    // outside the framework. Step 10 (Fractilizer + crystal bounce) sets
    // meteorite_shrunk flag.
    thread WatchT5Flag( "gongs_resonating", "t5_sl_sq_8", false );  // step 9 — Gongs + dynamite catch
    thread WatchT5Flag( "meteorite_shrunk", "t5_sl_sq_9", false );  // step 10 — Meteorite shrink (Fractilizer)

    // Step 11 — final altar / reward. BaG stage completion is what triggers
    // the existing temple_sidequest_achieved notify, so canonical fire is
    // handled by WaitForEasterEggComplete.
    thread WatchT5LevelNotify( "sq_BaG_completed",   "t5_sl_sq_10", false );  // step 11 — Final reward
}

/////////////////////////////////////////////////////////
// Moon (zombie_moon) — two parallel quests:
//
//   Song EE — three Element 115 fragments (community-known as bears for
//   "Coming Home"). Same poll-counter pattern as Coast / Shangri-La.
//   Reuses shared WatchT5MeteorCounterSong helper.
//
//   Richtofen's Grand Scheme — uses the stock _zombiemode_sidequests
//   framework: 8 stage-completion notifies across 3 sidequests:
//     - `sq` main quest    (ss1, osc, sc, sc2, ss2)
//     - `be` sub-sidequest (Bouncing Egg — Vril Sphere & Big Bang)
//     - `ctvg` sub-quest   (Charge The Vril Generator — supercharged step)
//   The `tanks` sub-sidequest (Charge The Tanks) is a script-level
//   prereq for ctvg but isn't surfaced as a discrete community step.
//
//   Canonical terminal is the existing `moon_sidequest_big_bang_achieved`
//   notify in WaitForEasterEggComplete (fired from sq.gsc::do_launch
//   after be_stage_two_completed). All step watchers here are
//   isCanonical: false — emitting from both paths would double-fire.
//
// Premium config sets MinPlayers=4 because the script hard-walls steps
// 6+ (per community walkthrough: "Any step after this will require
// 4 players to complete"). Step events still fire regardless; UI hides
// quest for sub-4P matches.
/////////////////////////////////////////////////////////
HookMoon()
{
    level endon( "end_game" );

    thread WatchT5MeteorCounterSong( "t5_mn_fragment" );
    thread HookMoonRichtofen();
}

HookMoonRichtofen()
{
    level endon( "end_game" );

    logprint( "[ZM-EE] Step watcher armed: t5_mn_rgs (8 Richtofen's Grand Scheme stages)\n" );

    // Stage notifies follow the framework convention `<sidequest>_<stage>_completed`.
    thread WatchT5LevelNotify( "sq_ss1_completed",         "t5_mn_rgs_1", false );  // step 2 — Samantha Says (1st)
    thread WatchT5LevelNotify( "sq_osc_completed",         "t5_mn_rgs_2", false );  // step 3 — Lab Hacking (Open Source Code)
    thread WatchT5LevelNotify( "be_stage_one_completed",   "t5_mn_rgs_3", false );  // step 4 — Vril Sphere (Bouncing Egg 1)
    thread WatchT5LevelNotify( "sq_sc_completed",          "t5_mn_rgs_4", false );  // step 5 — Cryogenic Slumber Party (Soul Catch)
    thread WatchT5LevelNotify( "ctvg_charge_completed",    "t5_mn_rgs_5", false );  // step 6 — Supercharged Vril Device
    thread WatchT5LevelNotify( "sq_sc2_completed",         "t5_mn_rgs_6", false );  // step 7 — Richtofen's Betrayal (Soul Swap)
    thread WatchT5LevelNotify( "sq_ss2_completed",         "t5_mn_rgs_7", false );  // step 8 — Samantha Says (2nd)
    thread WatchT5LevelNotify( "be_stage_two_completed",   "t5_mn_rgs_8", false );  // step 9 — Big Bang Theory
}
