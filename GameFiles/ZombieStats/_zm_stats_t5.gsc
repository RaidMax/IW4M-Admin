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
    logPrint( "ZM_STATS_T5 v17 loaded\n" );

    thread WaitForRoundChange();
    thread WaitForPlayerConnect();
    thread WaitForPowerupSpawned();
    thread WaitForWeaponPurchases();
    thread WaitForDoorPurchases();
    // thread WaitForMysteryBox(); // DISABLED — see box comments for details
    thread WaitForPackAPunch();
    thread WaitForTrapActivations();
    thread WaitForAutoTurrets();

    // --- Zombie Event Log Format --- //
    // Combat events (legacy format): AK, AD, K, D, RD, RC
    // Unified ZE format: down, revive, perk, powerup, weapon, box, door, trap

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

    logPrint( "GSE;RC;" + currentRound + "\n" );
}

/////////////////////////////////////////////////////////
// Economy event hooks — wall buys, box, PaP, doors, traps
// T5 uses entity-based trigger listeners (no level notifies
// for weapon_bought like T6)
/////////////////////////////////////////////////////////

// T5 Pack-a-Punch — POLLING APPROACH
//
// PaP on T5 uses "zombie_vending_upgrade" triggers, NOT the perk system.
// "perk_bought" with "specialty_weapupgrade" is NOT fired.
// "pap_taken" fires on the player (line 563 ref) but by the time our
// thread resumes, current_weapon is already the upgraded weapon.
//
// Solution: poll the PaP trigger's .current_weapon property (set when
// the player places their weapon in the machine). Capture it before
// the upgrade, then wait for it to clear (weapon collected or timeout).
// Same approach as T4's WatchPackAPunch.
WaitForPackAPunch()
{
    self endon( "disconnect" );

    // Find PaP triggers — persistent map entities
    wait ( 2 );

    triggers = getEntArray( "zombie_vending_upgrade", "targetname" );

    if ( !IsDefined( triggers ) )
    {
        return;
    }

    for ( i = 0; i < triggers.size; i++ )
    {
        triggers[i] thread WatchPackAPunch();
    }
}

WatchPackAPunch()
{
    for ( ;; )
    {
        // Wait for a weapon to be placed in the machine
        // current_weapon is set when the player puts their weapon in
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

        // Find the player closest to PaP
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

        // Wait for weapon to be collected or timeout (current_weapon cleared)
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
            logPrint( "GSE;ZE;" + BuildPlayerInfoString( closest ) + ";weapon;upgrade;" + oldWeapon + ";" + newWeapon + ";5000\n" );
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
// T5 Mystery Box Detection — CONTINUOUS POLLING APPROACH
//
// T5 is between T4 and T6 in terms of box scripting cleanliness:
//   - chest_user is set BEFORE randomization (unlike T4 which sets it AFTER)
//   - "user_grabbed_weapon" only fires on chest entity, not player (unlike T6)
//   - weapon_string on chest_origin cycles during animation, cleared on cleanup
//
// Since chest_user is set early, we have the full ~3.9s animation window
// to capture the user. For the weapon name, we continuously read
/////////////////////////////////////////////////////////
// T5 Mystery Box — DISABLED (not supported)
//
// T5's box scripting has fundamental timing issues that make reliable
// detection impractical without engine-level hooks. Problems encountered:
//
// 1. waittill("trigger") — exclusive per entity, consumed by game code.
//
// 2. waittill("user_grabbed_weapon") — only fires on chest entity (not
//    player like T6). GSC notify() resumes threads AFTER the notifying
//    code yields, by which point chest_user/weapon_string are cleared.
//
// 3. chest_user polling — T5 sets chest_user BEFORE randomization, but
//    on rapid re-pulls, chest_user goes undefined→defined within a single
//    poll interval (0.1s). The while loop never sees the gap, treating
//    consecutive box uses as one continuous use.
//
// 4. weapon_string stabilization (T4 approach) — T5's weapon_string on
//    chest_origin is cleared at animation start AND during cleanup. Hard
//    to distinguish "new animation starting" from "old use closing."
//
// 5. timedOut race condition — after timeout, timedOut=true persists for
//    ~3s (wait 3 in box closing). But when player re-triggers immediately,
//    the new treasure_chest_think resets timedOut=false in the same frame.
//    Even with continuous capture inside the polling loop, edge cases remain
//    where passes are logged as takes.
//
// 6. Teddy detection — treasure_chest_move is THREADED on T5 (line 1180),
//    so chest_user is cleared BEFORE weapon_fly_away_start fires.
//    WaitForBoxTeddy can't find the user. flag("moving_chest_now") works
//    for detection but has its own timing edge cases with rapid re-pulls.
//
// 7. T5 GSC does NOT short-circuit && or || — any IsDefined(x) && x
//    pattern crashes with "cannot cast undefined to bool" when x is
//    undefined. All conditionals must use nested ifs.
//
// WHAT WORKED:
//   - Take detection when player waits a few seconds between pulls
//   - Pass detection when player doesn't immediately re-trigger
//   - Weapon names were accurate via continuous weapon_string capture
//
// WHAT DIDN'T:
//   - Rapid consecutive pulls (pass-then-immediate-reopen logged as take)
//   - Teddy bear with F-spam (logged as take before flag was visible)
//   - Consistent detection across all play styles
//
// TO RESUME IN FUTURE:
//   - The core approach (poll chest_user + capture weapon_string + wasPass
//     + isTeddyBear) is sound for normal-speed play
//   - The rapid re-pull edge case needs a fundamentally different signal
//     (not chest_user or weapon_string, which both have sub-frame transitions)
//   - Consider: hooking the score deduction (player.score drops by 950),
//     tracking player weapon inventory changes, or adding a custom
//     level notify from a modified _zombiemode_weapons.gsc
//   - T6's approach (player-level "user_grabbed_weapon" notify) is the
//     cleanest — if T5 could be patched to add a similar player notify,
//     all issues would be resolved
//
// T4 box works because weapon_spawn_org.weapon_string changes DURING
// the animation with many wait() calls (3.9s of yields), giving ample
// polling window. T5 has similar animation but the state management
// around chest_user/timedOut is less predictable.
//
// T6 box works because it fires user_grabbed_weapon on the PLAYER
// entity, not just the chest — no competition with game code.
/////////////////////////////////////////////////////////
// WaitForMysteryBox() — disabled
// WatchBoxOutcome() — disabled
// WaitForBoxTeddy() — disabled

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
