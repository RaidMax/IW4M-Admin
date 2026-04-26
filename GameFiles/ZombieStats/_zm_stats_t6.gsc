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
    thread WaitForPackAPunch();
    thread WaitForDoorPurchases();
    thread WaitForMysteryBox();
    // Teddy detection moved into WatchBoxPass via flag("moving_chest_now").
    // WaitForBoxTeddy doesn't work — treasure_chest_move is THREADED,
    // so chest_user is cleared before weapon_fly_away_start fires.
    thread WaitForTrapActivations();
    thread WaitForBuildables();

    // --- Zombie Event Log Format --- //
    // Combat events (legacy format):
    //   AK, AD, K, D = kills/damage (unchanged)
    //   RD, RC = round data/complete (unchanged)
    //
    // Unified ZE format:
    //   ZE;{player};down                                    = player downed
    //   ZE;{player};revive;{reviver}                        = player revived
    //   ZE;{player};perk;buy;{perkName};{cost}              = perk purchased
    //   ZE;{player};powerup;grab;{powerupName}              = powerup grabbed
    //   ZE;{player};weapon;buy;{weaponName};{cost}          = wall weapon purchase
    //   ZE;{player};weapon;upgrade;{old};{new};{cost}       = pack-a-punch
    //   ZE;{player};box;take;{weaponName};{cost}            = box weapon taken
    //   ZE;{player};box;pass;{weaponName};{cost}            = box weapon passed
    //   ZE;{player};box;teddy;{cost}                        = teddy bear (box moves)
    //   ZE;{player};door;buy;{cost}                         = door/debris opened
    //   ZE;{player};trap;activate;{trapType};{cost}         = trap activated
    //   ZE;{player};build;complete;{buildableName}          = buildable completed

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

        // PaP moved to trigger-based polling in init() — see WatchPackAPunch

        // T6 fires "user_grabbed_weapon" on the PLAYER entity (not just chest),
        // so per-player waittill works without competing with chest trigger listeners
        player thread WatchBoxGrab();

        ///#
        // todo: remove — debug helpers
        // Uncomment lines as needed for testing.
        //
        // Give max points (must wait for spawn or game overwrites with default):
        //player thread DebugGiveScore();
        //
        // Disable out-of-bounds kill monitor. The monitor thread uses
        // self endon("stop_player_out_of_playable_area_monitor"), so
        // notifying it kills the thread. Also set the flag to 0 to
        // prevent it restarting on respawn.
        //level.player_out_of_playable_area_monitor = 0;
        //player notify( "stop_player_out_of_playable_area_monitor" );
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

        logprint( "GSE;ZE;" + BuildPlayerInfoString( self ) + ";revive;" + BuildPlayerInfoString( reviver ) + "\n" );
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

        // Perk cost is not available from the perk_bought notify.
        // The game's cost lookup is hardcoded in _zm_perks per-perk switch
        // statements — not exposed to external scripts.
        // Free perks (from free_perk powerup) don't fire perk_bought at all.
        logprint( "GSE;ZE;" + BuildPlayerInfoString( self ) + ";perk;buy;" + perk + ";0\n" );
    }
}

/////////////////////////////////////////////////////////
// Waits for "pap_taken" player notify fired by
// _zm_perks::vending_weapon_upgrade() when a player
// grabs their upgraded weapon from the PaP machine
/////////////////////////////////////////////////////////
// T6 Pack-a-Punch — POLLING APPROACH
//
// pap_taken fires on the player, but by the time our thread resumes,
// getCurrentWeapon() returns "none" (weapon not given yet).
// Same approach as T4/T5: poll the PaP trigger's .current_weapon
// property which is set when the player places their weapon.
WaitForPackAPunch()
{
    self endon( "disconnect" );

    wait ( 2 );

    // T6 PaP triggers come from two sources:
    //   1. targetname "zombie_vending" with script_noteworthy "specialty_weapupgrade"
    //   2. targetname "zombie_vending_upgrade" (legacy fallback)
    // Both get threaded with vending_weapon_upgrade() by the game.
    // We replicate the same lookup to find them.
    papTriggers = [];

    vendingTriggers = getEntArray( "zombie_vending", "targetname" );
    if ( IsDefined( vendingTriggers ) )
    {
        for ( i = 0; i < vendingTriggers.size; i++ )
        {
            if ( IsDefined( vendingTriggers[i].script_noteworthy ) )
            {
                if ( vendingTriggers[i].script_noteworthy == "specialty_weapupgrade" )
                {
                    papTriggers[papTriggers.size] = vendingTriggers[i];
                }
            }
        }
    }

    oldPacks = getEntArray( "zombie_vending_upgrade", "targetname" );
    if ( IsDefined( oldPacks ) )
    {
        for ( i = 0; i < oldPacks.size; i++ )
        {
            papTriggers[papTriggers.size] = oldPacks[i];
        }
    }

    if ( papTriggers.size == 0 )
    {
        return;
    }

    for ( i = 0; i < papTriggers.size; i++ )
    {
        papTriggers[i] thread WatchPackAPunch();
    }
}

// Polls the PaP trigger's .current_weapon property.
// Set when player places weapon in machine (line 630 ref),
// cleared after collection or timeout (line 645 ref).
// waittill("pap_taken") doesn't work — GSC resumes our thread
// AFTER the game clears current_weapon and gives the upgraded
// weapon, so we'd read empty state. Polling captures the
// weapon name while it's still valid.
WatchPackAPunch()
{
    for ( ;; )
    {
        // Wait for a weapon to be placed in the machine
        while ( true )
        {
            if ( IsDefined( self.current_weapon ) )
            {
                if ( self.current_weapon != "" )
                {
                    break;
                }
            }
            wait ( 0.2 );
        }

        oldWeapon = self.current_weapon;

        // Find closest player to PaP
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

        // Wait for weapon to be collected or timeout
        while ( true )
        {
            if ( !IsDefined( self.current_weapon ) )
            {
                break;
            }
            if ( self.current_weapon == "" )
            {
                break;
            }
            wait ( 0.2 );
        }

        if ( IsDefined( closest ) )
        {
            newWeapon = oldWeapon + "_upgraded";
            logprint( "GSE;ZE;" + BuildPlayerInfoString( closest ) + ";weapon;upgrade;" + oldWeapon + ";" + newWeapon + ";5000\n" );
        }
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

                logprint( "GSE;ZE;" + BuildPlayerInfoString( players[i] ) + ";powerup;grab;" + powerup + "\n" );

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
            // Cap reported damage at the victim's max HP — the engine can pass
            // iDamage values far in excess of what the zombie could actually absorb
            // (seen in Die Rise at round 30: MOD_PROJECTILE_SPLASH reporting ~5.5M/hit).
            reportedDamage = iDamage;
            if ( IsDefined( self.maxhealth ) && self.maxhealth > 0 && reportedDamage > self.maxhealth )
            {
                reportedDamage = self.maxhealth;
            }

            logprint( "GSE;AD;" + victimInfo +  ";" + attackerInfo + ";" + sWeapon + ";" + reportedDamage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
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

    logprint( "GSE;ZE;" + BuildPlayerInfoString( self ) + ";down\n" );

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

    setdvar( "sv_iw4m_zm_round", currentRound );
    logprint( "GSE;RC;" + currentRound + "\n" );
}

/////////////////////////////////////////////////////////
// Economy event hooks — wall buys, box, PaP, doors,
// traps, and buildables.
// T6 fires level-scoped notifies for most of these.
/////////////////////////////////////////////////////////

/////////////////////////////////////////////////////////
// Listens for "weapon_bought" level notify fired by
// _zm_weapons::vending_weapon_upgrade() on wall buys
/////////////////////////////////////////////////////////
WaitForWeaponPurchases()
{
    for ( ;; )
    {
        level waittill( "weapon_bought", player, weaponName );

        if ( !IsDefined( player ) || !IsPlayer( player ) )
        {
            continue;
        }

        // Cost stored in level.zombie_weapons table
        cost = 0;
        if ( IsDefined( level.zombie_weapons ) && IsDefined( level.zombie_weapons[weaponName] ) && IsDefined( level.zombie_weapons[weaponName].cost ) )
        {
            cost = level.zombie_weapons[weaponName].cost;
        }

        logprint( "GSE;ZE;" + BuildPlayerInfoString( player ) + ";weapon;buy;" + weaponName + ";" + cost + "\n" );
    }
}

/////////////////////////////////////////////////////////
// Listens for "door_opened" level notify fired by
// _zm_blockers when any door/debris is purchased
/////////////////////////////////////////////////////////
WaitForDoorPurchases()
{
    // Wait for doors to be initialized
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

    logprint( "GSE;ZE;" + BuildPlayerInfoString( player ) + ";door;buy;" + cost + "\n" );
}

/////////////////////////////////////////////////////////
// T6 Mystery Box Detection — PLAYER NOTIFY APPROACH
//
// T6 is cleaner than T4/T5. Key differences:
//   - user notify("user_grabbed_weapon") fires on the PLAYER entity
//     (not just the chest), so we can waittill on the player with
//     no competition from the game's chest trigger listeners.
//   - self.grab_weapon_name is set on the chest before the grab loop,
//     giving us a reliable weapon name.
//   - chest_user is set BEFORE randomization AND persists through
//     the grab/timeout cycle.
//
// This means we DON'T need the weapon_string stabilization hack
// used on T4/T5. Simple per-player waittill works.
//
// Take: player waittill("user_grabbed_weapon") — instant, no race.
// Pass: poll chest_user + timedOut (12s window, polling is fine).
// Teddy: level waittill("weapon_fly_away_start") + chest_user.
/////////////////////////////////////////////////////////
WaitForMysteryBox()
{
    // Wait for chests to be initialized by the game
    wait ( 5 );

    if ( !IsDefined( level.chests ) )
    {
        return;
    }

    for ( i = 0; i < level.chests.size; i++ )
    {
        level.chests[i] thread WatchBoxPass();
    }
}

// Spawned per-player from WaitForPlayerConnect.
// Listens for "user_grabbed_weapon" on the player entity — T6 fires
// this notify on both the chest AND the player (line 573 of reference).
// Player-level waittill has no competition, so it fires reliably.
WatchBoxGrab()
{
    self endon( "disconnect" );

    for ( ;; )
    {
        self waittill( "user_grabbed_weapon" );

        // Find which chest this player used
        if ( !IsDefined( level.chests ) )
        {
            continue;
        }

        weaponName = "unknown";
        cost = 950;

        for ( i = 0; i < level.chests.size; i++ )
        {
            chest = level.chests[i];
            if ( IsDefined( chest.chest_user ) && chest.chest_user == self )
            {
                if ( IsDefined( chest.grab_weapon_name ) )
                {
                    weaponName = chest.grab_weapon_name;
                }
                else if ( IsDefined( chest.zbarrier ) && IsDefined( chest.zbarrier.weapon_string ) )
                {
                    weaponName = chest.zbarrier.weapon_string;
                }

                if ( IsDefined( chest.zombie_cost ) )
                {
                    cost = chest.zombie_cost;
                }

                break;
            }
        }

        logprint( "GSE;ZE;" + BuildPlayerInfoString( self ) + ";box;take;" + weaponName + ";" + cost + "\n" );
    }
}

// Detects box passes (and suppresses false logs on teddies).
// chest_user is set BEFORE randomization
// on T6, so we have the full animation + timeout window to poll.
//
// grab_weapon_name persists from the previous use, so we continuously
// read zbarrier.weapon_string instead (cycles during animation, settles
// on the final weapon). Same continuous capture approach as T5.
//
// Teddy: treasure_chest_move is THREADED on T6 (line 521 ref), so
// chest_user is cleared before weapon_fly_away_start fires. Same as T5.
// We detect teddy via flag("moving_chest_now") during polling.
WatchBoxPass()
{
    for ( ;; )
    {
        while ( !IsDefined( self.chest_user ) || !IsPlayer( self.chest_user ) )
        {
            wait ( 0.2 );
        }

        user = self.chest_user;

        cost = 950;
        if ( IsDefined( self.zombie_cost ) )
        {
            cost = self.zombie_cost;
        }

        // Continuously capture weapon and teddy flag while box is open
        weaponName = "unknown";
        isTeddyBear = false;
        while ( IsDefined( self.chest_user ) )
        {
            if ( IsDefined( self.zbarrier ) )
            {
                if ( IsDefined( self.zbarrier.weapon_string ) )
                {
                    weaponName = self.zbarrier.weapon_string;
                }
            }

            if ( !isTeddyBear )
            {
                if ( flag( "moving_chest_now" ) )
                {
                    isTeddyBear = true;
                }
            }

            wait ( 0.1 );
        }

        // Only log passes — grabs handled by per-player WatchBoxGrab.
        // Teddies are suppressed (score refund captured implicitly via RD events).
        if ( !isTeddyBear )
        {
            if ( IsDefined( self.timedOut ) )
            {
                if ( self.timedOut )
                {
                    logprint( "GSE;ZE;" + BuildPlayerInfoString( user ) + ";box;pass;" + weaponName + ";" + cost + "\n" );
                }
            }
        }
    }
}

/////////////////////////////////////////////////////////
// Monitors for box movement (teddy bear) via level notify
/////////////////////////////////////////////////////////
WaitForBoxTeddy()
{
    for ( ;; )
    {
        level waittill( "weapon_fly_away_start" );

        // Find the player who last used the box
        if ( !IsDefined( level.chests ) )
        {
            continue;
        }

        for ( i = 0; i < level.chests.size; i++ )
        {
            if ( IsDefined( level.chests[i].chest_user ) && IsPlayer( level.chests[i].chest_user ) )
            {
                logprint( "GSE;ZE;" + BuildPlayerInfoString( level.chests[i].chest_user ) + ";box;teddy;0\n" );
                break;
            }
        }
    }
}

/////////////////////////////////////////////////////////
// T6 Trap Detection — POLLING _trap_in_use
//
// Same approach as T5. T6's _zm_traps only fires "trap_activate"
// notify from trap_activate_electric(). Fire, rotating, and flipper
// traps don't fire it. _trap_in_use is set to 1 on purchase for
// ALL trap types by the shared trap_think() function.
//
// Mob of the Dead's custom traps (fan/acid/tower) use their own
// system entirely — no zombie_trap targetname, no _trap_in_use.
// The IsSubStr("trap") scan catches their trigger entities but
// _trap_in_use won't be set. Those traps are not supported.
/////////////////////////////////////////////////////////
WaitForTrapActivations()
{
    // Wait for traps to be initialized
    wait ( 2 );

    // Standard T6 traps use "zombie_trap" targetname
    traps = getEntArray( "zombie_trap", "targetname" );

    for ( i = 0; i < traps.size; i++ )
    {
        traps[i] thread WatchTrapActivation();
    }

    // Also scan for non-standard trap triggers (e.g. Mob of the Dead:
    // fan_trap_use_trigger, acid_trap_trigger, tower_trap_activate_trigger)
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
// T6 (like T5) only fires "trap_activate" from trap_activate_electric().
// Fire, rotating, flipper traps don't fire it. _trap_in_use is set to 1
// on purchase for ALL trap types.
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

        // Find who activated it from the connected players
        // The game stores the activator context but doesn't pass it with the notify,
        // so we check who most recently triggered the use trigger
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
            logprint( "GSE;ZE;" + BuildPlayerInfoString( closest ) + ";trap;activate;" + trapType + ";" + cost + "\n" );
        }

        // Wait for trap to finish and cool down before re-polling
        while ( true )
        {
            if ( IsDefined( self._trap_in_use ) )
            {
                if ( self._trap_in_use != 1 )
                {
                    break;
                }
            }
            wait ( 1 );
        }
    }
}

/////////////////////////////////////////////////////////
// Monitors buildable completion via level notifies
// T6-specific: buildables fire "<name>_built" on level
/////////////////////////////////////////////////////////
WaitForBuildables()
{
    // Wait for buildable system to initialize
    wait ( 3 );

    if ( !IsDefined( level.zombie_buildables ) )
    {
        return;
    }

    names = getArrayKeys( level.zombie_buildables );

    for ( i = 0; i < names.size; i++ )
    {
        thread WatchBuildableComplete( names[i] );
    }
}

WatchBuildableComplete( buildableName )
{
    for ( ;; )
    {
        level waittill( buildableName + "_built", player );

        if ( !IsDefined( player ) || !IsPlayer( player ) )
        {
            continue;
        }

        logprint( "GSE;ZE;" + BuildPlayerInfoString( player ) + ";build;complete;" + buildableName + "\n" );
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
        name = entity.name;

        if ( !IsDefined( name ) )
        {
            name = "null";
        }

        return guid + ";" + clientNumber + ";" + team + ";" + name;
    }

    return "-1;-1;axis;Zombie";
}
