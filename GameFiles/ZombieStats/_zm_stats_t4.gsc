#include maps\_utility; 
#include common_scripts\utility; 
#include maps\_zombiemode_utility; 

Init()
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
    // gettime() returns 0 at Init time on T4 (engine clock not yet running),
    // so we don't use it here. The lookup index is (ServerId, GameMatchId)
    // so cross-server collisions are harmless either way.
    // Set once per Init (= once per map load).
    setdvar( "sv_iw4m_zm_matchid", "" + randomint( 1000000 ) + "_" + randomint( 1000000 ) );

    thread WaitForRoundChange();
    thread WaitForPlayerConnect();
    thread WaitForPowerupSpawned();
    thread WaitForWeaponPurchases();
    thread WaitForPackAPunch();
    thread WaitForDoorPurchases();
    // Box detection: Der Riese + Der-Riese-derived custom maps (anything that
    // ports Der Riese's _zombiemode_weapons.gsc). No map-name check — gating
    // is on `IsDefined(self.timedOut)` per chest, which only Der-Riese-style
    // chests assign. Other stock T4 maps silently no-op. See the long header
    // comment above WaitForMysteryBox for the full design.
    thread WaitForMysteryBox();
    thread WaitForBoxTeddySuppression();
    thread WaitForTrapActivations();

    // --- Zombie Event Log Format --- //
    // Combat events (legacy format): AK, AD, K, D, RD, RC
    // Unified ZE format: down, revive, perk, powerup, weapon, box, door, trap

    SetupCallbacks();
}

SetupCallbacks()
{
    waittillframeend;
    
    // zombie damage events
    level.callbackActorDamageOriginal = level.callbackActorDamage;
    level.callbackActorKilledOriginal = level.callbackActorKilled;
    level.callbackActorDamage = ::OnActorDamage;
    level.callbackActorKilled = ::OnActorKilled;

    // player damage events
    level.callbackPlayerDamageOriginal = level.callbackPlayerDamage;
    level.callbackPlayerDamage = ::OnPlayerDamaged;

    // not used in zm
    // level.callbackPlayerKilledOriginal = level.callbackPlayerKilled;
    // level.callbackPlayerKilled = ::OnPlayerKilled;

    // down/revive events
    level.callbackPlayerLastStandOriginal = level.callbackPlayerLastStand;
    level.callbackPlayerLastStand = ::OnPlayerDowned;

    // powerup event 
    // not used as implementing disables regular function
    // level.zombie_powerup_grab_func = ::OnPowerupGrabbed;
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
        
        // the PlayerRevived callback is not setup to allow overriding
        // so we need to wait for the hard-coded player notify
        player thread WaitForPlayerRevive();

        // verruckt does not track the perks as stats like der reise and shi no,
        // so we wait for the weapon to switch to perk weapon bottle
        player thread WaitForPlayerWeaponSwitch();

        // zm mode does not actually kill a player after down timer expires
        // they get put into spectator without a kill callback
        player thread WaitForPlayerZombified();
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
        
        LogPrint( "GSE;K;" + playerInfo + ";-1;-1;axis;Zombie;default_weapon;0;MOD_MELEE;none\n");
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
        self waittill( "player_revived", reviver );

        // give time for the weapon switch to occur
        wait ( 0.1 ); 

        if ( !IsDefined( reviver ) || !IsPlayer( reviver ) )
        {
            players = get_players();

            // reviver only passed on der riese, so we have check if anyone
            // has used the revive weapon recently instead
            for ( i = 0; i < players.size; i++ )
            {
                didPerformRevive = IsPlayer( players[i] ) && 
                    IsDefined( players[i].lastUsedSyrette ) && 
                    gettime() - players[i].lastUsedSyrette <= 250;

                if ( didPerformRevive )
                {
                    reviver = players[i];
                    players[i].lastUsedSyrette = 0;
                    break;
                }
            }
        }
        
        LogPrint( "GSE;ZE;" + BuildPlayerInfoString( self ) + ";revive;" + BuildPlayerInfoString( reviver ) + "\n" );
    }
}

/////////////////////////////////////////////////////////
// Waits until player changes weapons and checks to see
// if weapon is a perk weapon. Prints to gamelog if true
/////////////////////////////////////////////////////////
WaitForPlayerWeaponSwitch()
{
    self endon( "disconnect" );

    self waittill( "spawned_player" );
    currentWeapon = self getCurrentWeapon();

    ///#
    // todo: remove
    //self.score = 1000000;
    //#/

    for ( ;; )
    {
        wait ( 0.1 );

        if ( !IsAlive ( self ) )
        {
            continue;
        }

        newWeapon = self getCurrentWeapon();

        if ( currentWeapon != newWeapon )
        {
            if ( currentWeapon == "syrette" )
            {
                self.lastUsedSyrette = gettime();
            }

            currentWeapon = newWeapon;

            if ( IsSubStr( currentWeapon, "zombie_perk" ) )
            {
                LogPrint( "GSE;ZE;" + BuildPlayerInfoString( self ) + ";perk;buy;" + currentWeapon + ";0\n" );
            }
        }
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
        players = Get_Players();

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

                LogPrint( "GSE;ZE;" + BuildPlayerInfoString( players[i] ) + ";powerup;grab;" + powerup + "\n" );

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
        PrintLn( "WaitForRoundStart TRIGGERED" );
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
            if ( Get_Enemy_Count() == 0 )
            {
                continue;
            }

            // game is over so we print out their death
            playerInfo = BuildPlayerInfoString( players[i] );
            LogPrint( "GSE;K;" + playerInfo + ";-1;-1;axis;Zombie;default_weapon;0;MOD_MELEE;none\n");
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

            LogPrint( "GSE;AD;" + victimInfo +  ";" + attackerInfo + ";" + sWeapon + ";" + reportedDamage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
        }
    }

    [[level.callbackActorDamageOriginal]]( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, iModelIndex, iTimeOffset );
}

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

        LogPrint( "GSE;AK;" + victimInfo + ";" + attackerInfo + ";" + sWeapon + ";" + damage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
    }

    [[level.callbackActorKilledOriginal]]( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, iTimeOffset );
}

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

        LogPrint( "GSE;D;" + victimInfo + ";" + attackerInfo + ";" + sWeapon + ";" + iDamage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" ); 
    }

    [[level.callbackPlayerDamageOriginal]]( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, iModelIndex, timeOffset );
}

OnPlayerDowned( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration )
{
    // sometimes this callback can be executed multiple times while the player is still downed
    // this struct is set to undefined when they die or get revived
    if ( IsDefined( self.revivetrigger ) )
    {    
        return;
    }

    LogPrint( "GSE;ZE;" + BuildPlayerInfoString( self ) + ";down\n" );

    [[level.callbackPlayerLastStandOriginal]]( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration );
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

        LogPrint( "GSE;RD;" + BuildPlayerInfoString( players[i] ) + ";" + totalScore + ";" + currentScore + ";" + currentRound + ";" + isGameOver + "\n" );
    }

    // Ensure all RD events are processed before RC triggers StartNextRound
    // which clears round states. Without this wait, RC can race ahead of
    // late-arriving RD events due to IW4MAdmin's concurrent event processing.
    wait ( 0.1 );

    setdvar( "sv_iw4m_zm_round", currentRound );
    LogPrint( "GSE;RC;" + currentRound + "\n" );
}

/////////////////////////////////////////////////////////
// Economy event hooks — wall buys, box, doors, traps
// T4 uses entity-based trigger listeners.
// No buildables in T4. Traps are per-map (electric only).
/////////////////////////////////////////////////////////

WaitForWeaponPurchases()
{
    wait ( 2 );

    triggers = getEntArray( "weapon_upgrade", "targetname" );

    for ( i = 0; i < triggers.size; i++ )
    {
        triggers[i] thread WatchWeaponPurchase();
    }
}

// Wall buy triggers use targetname "weapon_upgrade" and have a
// .zombie_weapon_upgrade property with the weapon name. Cost is in
// level.zombie_weapons[weaponName].cost (NOT on the trigger entity —
// trigger.zombie_cost is only used for cabinet first-purchase pricing).
//
// Note: Pack-a-Punch is handled separately by WaitForPackAPunch.
// PaP uses "zombie_vending_upgrade" triggers — a completely different
// entity type. Earlier versions tried to detect PaP here via
// IsSubStr("_upgraded") but wall buy triggers never have upgraded
// weapon names on them.
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

        cost = 0;
        if ( IsDefined( level.zombie_weapons ) && IsDefined( level.zombie_weapons[weaponName] ) && IsDefined( level.zombie_weapons[weaponName].cost ) )
        {
            cost = level.zombie_weapons[weaponName].cost;
        }
        else if ( IsDefined( self.zombie_cost ) )
        {
            cost = self.zombie_cost;
        }

        // Trigger fires on any interaction, even if player can't afford it
        if ( IsDefined( player.score ) && player.score < cost )
        {
            continue;
        }

        LogPrint( "GSE;ZE;" + BuildPlayerInfoString( player ) + ";weapon;buy;" + weaponName + ";" + cost + "\n" );
    }
}

/////////////////////////////////////////////////////////
// T4 Pack-a-Punch uses "zombie_vending_upgrade" triggers,
// NOT the "weapon_upgrade" triggers used by wall buys.
//
// Flow in _zombiemode_perks::vending_upgrade():
//   1. Player triggers PaP → vending_upgrade() consumes trigger
//   2. 5000 points deducted, weapon placed in machine
//   3. self.current_weapon = original weapon name
//   4. Machine animates (third_person_weapon_upgrade)
//   5. Trigger re-enabled for player to collect
//   6. Player triggers again → "pap_taken" fires on trigger entity
//   7. Player receives weapon + "_upgraded"
//
// We listen for "pap_taken" which fires BEFORE the upgraded weapon
// is given and while self.current_weapon still holds the original.
// Cannot use waittill("trigger") — vending_upgrade() consumes it.
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
        triggers[i] thread WatchPackAPunch();
    }
}

// IMPORTANT — why we poll instead of using waittill("pap_taken"):
//
// In GSC, self notify("pap_taken") schedules our waiting thread to resume,
// but it doesn't actually run until the notifying code yields. By then,
// vending_upgrade() has already executed self.current_weapon = "" (cleanup).
// Our thread resumes with current_weapon already cleared → "unknown".
//
// Instead, we poll for self.current_weapon being set (happens when player
// places weapon in machine, persists for several seconds during animation).
// Then we poll for it being cleared (happens after player takes or timeout).
// We capture the weapon name while it's still valid.
WatchPackAPunch()
{
    for ( ;; )
    {
        // Wait for a weapon to be placed in the machine
        while ( !IsDefined( self.current_weapon ) || self.current_weapon == "" )
        {
            wait ( 0.2 );
        }

        oldWeapon = self.current_weapon;

        // Find the player closest to PaP — they placed the weapon
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

        // Wait for the weapon to be taken or timeout (current_weapon cleared)
        while ( IsDefined( self.current_weapon ) && self.current_weapon != "" )
        {
            wait ( 0.2 );
        }

        // Only log if we found a player (skip if nobody nearby)
        if ( IsDefined( closest ) )
        {
            newWeapon = oldWeapon + "_upgraded";
            LogPrint( "GSE;ZE;" + BuildPlayerInfoString( closest ) + ";weapon;upgrade;" + oldWeapon + ";" + newWeapon + ";5000\n" );
        }
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
// The game checks score internally before opening. We must do the same check
// to avoid logging failed purchase attempts.
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

    LogPrint( "GSE;ZE;" + BuildPlayerInfoString( player ) + ";door;buy;" + cost + "\n" );
}

/////////////////////////////////////////////////////////
// T4 Mystery Box Detection (Der Riese + Der-Riese-derived custom maps).
//
// Engine reference: `_zombiemode_weapons.gsc` from the official T4 Der Riese
// scripts. Key entities/state:
//   - Chest trigger: targetname "treasure_chest_use" (all stock T4 maps)
//   - Entity chain: chest → lid (self.target) → weapon_spawn_org (lid.target)
//   - weapon_spawn_org.weapon_string: cycles ~40 times during the ~3.9s
//     randomization animation; final value persists until the next pull
//   - self.chest_user: assigned ~immediately after randomization_done, on
//     Der-Riese-derived chests ONLY (Nacht/Verrückt/Shi No Numa never set it,
//     and on Der Riese the teddy-bear branch in treasure_chest_think skips
//     the assignment). Cleared on grab or 12s timeout.
//   - self.timedOut: assigned to false at the top of every treasure_chest_think
//     iteration on Der-Riese-derived chests, true after 12s no-grab timeout.
//     Used here as the per-chest "is Der-Riese-derived" discriminator (no
//     map-name check).
//   - "trigger" notify on the chest entity: fires whenever a player presses
//     USE on the chest (both BUY phase and GRAB phase).
//   - level "weapon_fly_away_start" notify: fires ~0.5s after randomization_done
//     on the teddy-bear path only.
//
// Why three resolution sources for the buyer:
//
// `chest_user` is set and cleared inside one cooperative-scheduler frame on
// instant ("F-spammed") grabs — treasure_chest_think runs L797 (set), L808
// (waittill trigger, immediately resumed by buffered trigger), L838 (clear)
// without any wait between them. Polling at 0.1s cannot observe state that
// doesn't survive cross-frame, so polling alone misses every instant grab.
//
// We therefore combine:
//   1. Phase-2 polling capture of chest_user — wins when the player is
//      patient enough that chest_user lives across at least one tick.
//   2. Live chest_user read in Phase 3 — cheap fallback for the rare case
//      where the poll timing aligned but the state was missed.
//   3. Trigger-waittill capture of the most recent USE press, used as
//      ground-truth fallback ONLY when the above two failed AND the chest
//      is Der-Riese-derived (`IsDefined(self.timedOut)`).
//
// Teddy bears: the engine fires `level notify("weapon_fly_away_start")` when
// the box becomes a teddy. We mark the currently-active chest's
// `iw4m_box_teddy_marker` from a global suppression thread, then in Phase 3
// emit a `box;teddy;{cost}` event (matches T6's emit format) using the trigger
// fallback as the buyer (chest_user is never set on teddy, so paths 1+2 always
// miss). The marker is scoped to chests in late phase (Phase 2/3) so it can't
// bleed across iterations.
//
// Per-chest state used by this subsystem:
//   - self.iw4m_box_teddy_marker    — set by suppression, consumed in Phase 3
//   - self.iw4m_box_in_late_phase   — true between Phase 1 exit and iter end
//   - self.iw4m_box_last_trigger    — most recent player who pressed USE on
//                                     this chest; reset at iter end
//
// Map gating: nothing is gated on `level.script`. Stock Nacht/Verrückt/
// Shi No Numa silently no-op because their treasure_chest_think never assigns
// `self.timedOut`, so the trigger fallback's gate fails and Phase 1+2 never
// see a chest_user to capture. Custom maps that port Der Riese's chest code
// inherit both signals and work transparently.
/////////////////////////////////////////////////////////
WaitForMysteryBox()
{
    wait ( 5 );

    chests = getEntArray( "treasure_chest_use", "targetname" );

    if ( !IsDefined( chests ) || chests.size == 0 )
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
// Teddy bear suppression.
//
// The level-scoped notify is too broad on its own — marking every chest
// indiscriminately caused stale markers to bleed across iterations (an idle
// chest that hadn't entered Phase 1 since the previous teddy carried the
// mark forward and false-skipped its next pull). We therefore only mark
// chests with `iw4m_box_in_late_phase == true`, which is set by
// WatchBoxOutcome on Phase 1 exit and cleared at iteration end. On Der Riese
// only one chest is active at a time, so in practice exactly one chest gets
// marked per teddy event.
/////////////////////////////////////////////////////////
WaitForBoxTeddySuppression()
{
    wait ( 5 );
    for ( ;; )
    {
        level waittill( "weapon_fly_away_start" );

        chests = getEntArray( "treasure_chest_use", "targetname" );
        if ( !IsDefined( chests ) || chests.size == 0 )
        {
            continue;
        }

        for ( k = 0; k < chests.size; k++ )
        {
            if ( IsDefined( chests[k].iw4m_box_in_late_phase ) && chests[k].iw4m_box_in_late_phase )
            {
                chests[k].iw4m_box_teddy_marker = true;
            }
        }
    }
}

/////////////////////////////////////////////////////////
// Parallel ground-truth capture of who pressed USE on the chest.
//
// `treasure_chest_think` consumes "trigger" via `self waittill("trigger", user)`
// to read the buyer. GSC's notify model lets multiple threads waittill on the
// same notify and all of them resume — so this thread peacefully co-exists,
// stashing the most recent player on `self.iw4m_box_last_trigger`. Phase 3
// uses this as the third-tier resolution source for instant-grab cases where
// chest_user couldn't be polled in time, and as the only available source for
// teddy attribution.
//
// The trigger fires for both BUY and GRAB presses; latest wins. Failed-
// affordability presses also fire trigger but the engine enforces buyer ==
// grabber on Der Riese, so the value is correct either way.
/////////////////////////////////////////////////////////
WatchBoxTriggerForBuyer()
{
    for ( ;; )
    {
        self waittill( "trigger", who );
        if ( !IsDefined( who ) || !IsPlayer( who ) )
        {
            continue;
        }
        self.iw4m_box_last_trigger = who;
    }
}

WatchBoxOutcome()
{
    // Cache the weapon spawn origin entity — persistent map entity.
    lid = getent( self.target, "targetname" );
    if ( !IsDefined( lid ) )
    {
        return;
    }

    weaponSpawnOrg = getent( lid.target, "targetname" );
    if ( !IsDefined( weaponSpawnOrg ) )
    {
        return;
    }

    // Sentinel value — never matches any real weapon name.
    // On first use, weapon_string is undefined. Comparing undefined != "__none__"
    // would short-circuit incorrectly, so we guard with IsDefined.
    // On subsequent iterations, prevWeapon holds the final weapon from the
    // previous box use (kept stable until the next animation starts).
    prevWeapon = "__none__";

    for ( ;; )
    {
        // Phase 1: Wait for weapon_string to START changing.
        // During randomization the engine cycles through ~40 weapons over
        // ~3.9s (wait gaps 0.05–0.3s). Detecting the change is our signal
        // that a player just bought the box.
        animStarted = false;
        while ( !animStarted )
        {
            if ( IsDefined( weaponSpawnOrg.weapon_string ) )
            {
                if ( weaponSpawnOrg.weapon_string != prevWeapon )
                {
                    animStarted = true;
                }
                else
                {
                    prevWeapon = weaponSpawnOrg.weapon_string;
                }
            }

            if ( !animStarted )
            {
                wait ( 0.1 );
            }
        }

        // Mark this chest as the currently-active one. The teddy suppression
        // thread uses this flag to scope its marking — without it, the
        // level-wide notify would mark every chest and idle chests stuck in
        // Phase 1 would carry stale teddy marks until their next purchase.
        self.iw4m_box_in_late_phase = true;

        // Phase 2: Wait for weapon_string to STABILIZE (5 polls × 0.1s of
        // unchanged value). While polling, opportunistically capture
        // chest_user as soon as it's observed — this is best-effort because
        // chest_user can be set+cleared inside one frame on instant grabs.
        stableFrames = 0;
        stableWeapon = weaponSpawnOrg.weapon_string;
        capturedUser = undefined;
        while ( stableFrames < 5 )
        {
            wait ( 0.1 );

            if ( IsDefined( weaponSpawnOrg.weapon_string ) && weaponSpawnOrg.weapon_string == stableWeapon )
            {
                stableFrames = stableFrames + 1;
            }
            else
            {
                stableFrames = 0;
                stableWeapon = weaponSpawnOrg.weapon_string;
            }

            if ( !IsDefined( capturedUser ) && IsDefined( self.chest_user ) && IsPlayer( self.chest_user ) )
            {
                capturedUser = self.chest_user;
            }
        }

        weaponName = stableWeapon;

        cost = 950;
        if ( IsDefined( level.zombie_treasure_chest_cost ) )
        {
            cost = level.zombie_treasure_chest_cost;
        }
        else if ( IsDefined( self.zombie_cost ) )
        {
            cost = self.zombie_cost;
        }

        // Phase 3: Determine outcome. Resolution order documented in the
        // header comment above.
        timedOutDefined = 0;
        if ( IsDefined( self.timedOut ) )
        {
            timedOutDefined = 1;
        }

        user = capturedUser;
        if ( !IsDefined( user ) && IsDefined( self.chest_user ) && IsPlayer( self.chest_user ) )
        {
            user = self.chest_user;
        }

        // Wait for box to close. Normal grab: loops until engine clears
        // chest_user (~when player presses USE). Teddy / no-user iteration:
        // exits immediately because chest_user was never set.
        while ( IsDefined( self.chest_user ) )
        {
            wait ( 0.1 );
        }

        // If we still have no user, give the teddy notify time to arrive
        // before the trigger fallback decides. weapon_fly_away_start fires
        // ~0.5s after randomization_done; our Phase 2 already burned ~0.5s
        // on stabilization, so the notify lands within this 1s window. Normal
        // grabs (user already resolved) skip this wait.
        if ( !IsDefined( user ) )
        {
            wait ( 1.0 );
        }

        isTeddy = false;
        if ( IsDefined( self.iw4m_box_teddy_marker ) && self.iw4m_box_teddy_marker )
        {
            isTeddy = true;
        }
        self.iw4m_box_teddy_marker = false;

        // Trigger fallback. Resolves the buyer for two distinct cases:
        //   - Instant-grab take/pass: Phase 2 missed chest_user, the trigger
        //     waittill thread captured the buyer; emit normal take/pass.
        //   - Teddy: chest_user never set on the teddy path, the trigger
        //     thread is the only signal for who paid; emit teddy event.
        // Gated on `IsDefined(self.timedOut)` — only Der-Riese-derived
        // chests reach this path.
        if ( !IsDefined( user ) && timedOutDefined == 1
             && IsDefined( self.iw4m_box_last_trigger ) && IsPlayer( self.iw4m_box_last_trigger ) )
        {
            user = self.iw4m_box_last_trigger;
        }

        if ( IsDefined( user ) )
        {
            if ( isTeddy )
            {
                // Teddy bear: cost field only (no weapon — engine refunded
                // the 950). Matches T6's `box;teddy;{cost}` emit shape.
                LogPrint( "GSE;ZE;" + BuildPlayerInfoString( user ) + ";box;teddy;" + cost + "\n" );
            }
            else
            {
                isPass = false;
                if ( IsDefined( self.timedOut ) && self.timedOut )
                {
                    isPass = true;
                }

                if ( isPass )
                {
                    LogPrint( "GSE;ZE;" + BuildPlayerInfoString( user ) + ";box;pass;" + weaponName + ";" + cost + "\n" );
                }
                else
                {
                    LogPrint( "GSE;ZE;" + BuildPlayerInfoString( user ) + ";box;take;" + weaponName + ";" + cost + "\n" );
                }
            }
        }

        // Per-iteration cleanup.
        self.iw4m_box_last_trigger = undefined;
        self.iw4m_box_in_late_phase = false;

        // Seed prevWeapon with this iteration's final weapon so Phase 1 of
        // the next iteration correctly waits for the NEXT animation start.
        prevWeapon = weaponName;
    }
}

/////////////////////////////////////////////////////////
// T4 traps are per-map with unique targetnames:
//   Der Riese: warehouse_electric_trap, wuen_electric_trap, bridge_electric_trap
//   Verrückt:  gas_access
//   Shi No Numa: elec_trap_trig, pendulum_buy_trigger
//
// Instead of listing every name, scan all trigger_use entities
// and match targetnames containing "trap" or known keywords.
// This catches custom map traps too (anything with "trap" in name).
/////////////////////////////////////////////////////////
WaitForTrapActivations()
{
    wait ( 2 );

    allTriggers = getEntArray( "trigger_use", "classname" );

    for ( i = 0; i < allTriggers.size; i++ )
    {
        if ( !IsDefined( allTriggers[i].targetname ) )
        {
            continue;
        }

        name = allTriggers[i].targetname;

        // Match any trigger with "trap" in the name (covers electric_trap, fan_trap, acid_trap, etc.)
        // Also match known non-standard names:
        //   Verrückt: gas_access (electric traps)
        //   Shi No Numa: pendulum_buy_trigger (flogger trap)
        if ( IsSubStr( name, "trap" ) || name == "gas_access" || name == "pendulum_buy_trigger" )
        {
            allTriggers[i] thread WatchTrapActivation();
        }
    }
}

WatchTrapActivation()
{
    cost = 1000;
    if ( IsDefined( self.zombie_cost ) )
    {
        cost = self.zombie_cost;
    }

    for ( ;; )
    {
        self waittill( "trigger", player );

        if ( !IsDefined( player ) || !IsPlayer( player ) )
        {
            continue;
        }

        LogPrint( "GSE;ZE;" + BuildPlayerInfoString( player ) + ";trap;activate;electric;" + cost + "\n" );
    }
}

//-----------------------//
//---- Utility/Infra ----//
//-----------------------//

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
