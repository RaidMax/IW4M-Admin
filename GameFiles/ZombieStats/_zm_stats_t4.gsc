#include maps\_utility; 
#include common_scripts\utility; 
#include maps\_zombiemode_utility; 

Init()
{
    thread WaitForRoundChange();
    thread WaitForPlayerConnect();
    thread WaitForPowerupSpawned();
    thread WaitForWeaponPurchases();
    thread WaitForPackAPunch();
    thread WaitForDoorPurchases();
    // Box detection only works reliably on Der Riese (nazi_zombie_factory).
    // Earlier maps (Nacht, Verrückt, Shi No Numa) don't set chest_user —
    // they use treasure_chest_user_hint instead. Without chest_user,
    // grab_weapon_hint is the only signal, but it transitions within a
    // single frame on grabs, making take/pass indistinguishable via polling.
    // Der Riese's chest_user persists long enough for reliable detection.
    if ( IsDefined( level.script ) && level.script == "nazi_zombie_factory" )
    {
        thread WaitForMysteryBox();
        thread WaitForBoxTeddySuppression();
    }
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
// T4 Mystery Box Detection — WEAPON STRING STABILIZATION
//
// Three approaches were tried and failed before arriving here:
//
// 1. waittill("trigger") — FAILS. GSC waittill is exclusive per
//    entity per notify. treasure_chest_think() already consumes
//    the trigger notify. Our listener never fires.
//
// 2. waittill("user_grabbed_weapon") — FAILS. GSC notify()
//    schedules waiting threads to resume, but they don't run
//    until the notifying code yields. By then, chest_user and
//    weapon_string are already cleared.
//
// 3. Polling chest_user — FAILS for fast grabs. chest_user is
//    set AFTER the ~3.9s randomization animation completes, on
//    the same frame the trigger re-enables. F-spamming players
//    grab instantly, so chest_user is set AND cleared in one
//    frame — before any poll interval can catch it.
//
// SOLUTION: poll weapon_spawn_org.weapon_string instead.
//   - weapon_string changes DURING the animation (40 cycles, ~3.9s
//     of wait() calls), giving a wide detection window.
//   - We detect animation start (weapon_string differs from last
//     known value), then wait for stabilization (0.5s of no change
//     = animation done, final weapon chosen).
//   - For player identity: try chest_user first. If already cleared
//     (fast grab), fall back to nearest player.
//   - For outcome: chest_user + timedOut if available. If chest_user
//     was missed, it's always a "take" (timeouts are 12s).
//
// Teddy bear handled separately by WaitForBoxTeddy (proximity-based,
// because T4's teddy code path never sets chest_user).
//
// T4 entity layout:
//   - Chest trigger: targetname "treasure_chest_use"
//   - Entity chain: chest → lid (self.target) → weapon_spawn_org (lid.target)
//   - weapon_spawn_org.weapon_string: cycles during animation, final value persists
//   - self.chest_user: set after randomization, cleared on grab/timeout
//   - self.timedOut: false at box open, true after 12s timeout
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
        chests[i] thread WatchBoxOutcome();
    }
}

WatchBoxOutcome()
{
    // Cache the weapon spawn origin entity — it's a persistent map entity
    // Entity chain: chest (self) → lid (self.target) → weapon_spawn_org (lid.target)
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
    // On first use, weapon_string is undefined. Comparing undefined == "__none__"
    // could be unreliable in GSC, so we also guard with IsDefined.
    // On subsequent uses, prevWeapon holds the final weapon from the last box use.
    prevWeapon = "__none__";

    for ( ;; )
    {
        // Phase 1: Wait for weapon_string to START changing.
        // During randomization, weapon_string cycles through 40 random
        // weapons over ~3.9s (wait gaps: 0.05s→0.1s→0.2s→0.3s).
        //
        // prevWeapon is updated inside the loop so it tracks the current
        // stable value between box uses. When a new animation starts and
        // weapon_string changes to a different weapon, the loop breaks.
        //
        // First use:  prevWeapon = "__none__", weapon_string undefined → wait.
        //             weapon_string becomes "wp1" → doesn't match "__none__" → break.
        // Subsequent: prevWeapon = last final weapon (matches current stable value).
        //             New animation starts, weapon_string changes → break.
        // T4 GSC does NOT short-circuit || — both sides always evaluate.
        // So we must use nested ifs instead to guard against undefined.
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

        // Phase 2: Wait for weapon_string to STABILIZE (stop changing).
        // The animation cycles have gaps of 0.05s–0.3s between changes.
        // After the final weapon is chosen (cycle 40), no more changes.
        // If weapon_string is unchanged for 0.5s, animation is done.
        stableFrames = 0;
        stableWeapon = weaponSpawnOrg.weapon_string;
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
        }

        // weapon_string now has the FINAL weapon value
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

        // Phase 3: Determine outcome (Der Riese only).
        //
        // Der Riese sets chest_user after randomization. We capture the
        // user, then wait for chest_user to clear (grab or timeout).
        //
        // timedOut is checked AFTER the while loop exits, not inside it.
        // Both timedOut=true and chest_user=undefined happen in the same
        // frame (no waits between them), so our poll never sees timedOut=true
        // while chest_user is still defined. BUT timedOut persists after
        // chest_user clears — the new treasure_chest_think doesn't start
        // until after a `wait 3`, so timedOut is still readable here.
        user = undefined;
        if ( IsDefined( self.chest_user ) )
        {
            if ( IsPlayer( self.chest_user ) )
            {
                user = self.chest_user;
            }
        }

        // Wait for box to close
        while ( IsDefined( self.chest_user ) )
        {
            wait ( 0.1 );
        }

        // If chest_user was never caught (fast grab), find nearest player
        if ( !IsDefined( user ) )
        {
            players = get_players();
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
                    user = players[i];
                }
            }
        }

        if ( IsDefined( user ) )
        {
            // Check timedOut — still valid here because the new
            // treasure_chest_think doesn't re-thread until after wait 3.
            isPass = false;
            if ( IsDefined( self.timedOut ) )
            {
                if ( self.timedOut )
                {
                    isPass = true;
                }
            }

            if ( isPass )
            {
                LogPrint( "GSE;ZE;" + BuildPlayerInfoString( user ) + ";box;pass;" + weaponName + ";" + cost + "\n" );
            }
            else
            {
                // Wait for teddy suppression flag before logging take
                wait ( 1.5 );

                isTeddy = false;
                if ( IsDefined( self._zm_stats_teddy ) )
                {
                    if ( self._zm_stats_teddy )
                    {
                        isTeddy = true;
                        self._zm_stats_teddy = false;
                    }
                }

                if ( !isTeddy )
                {
                    LogPrint( "GSE;ZE;" + BuildPlayerInfoString( user ) + ";box;take;" + weaponName + ";" + cost + "\n" );
                }
            }
        }

        // Set prevWeapon to the final weapon so the next iteration's Phase 1
        // correctly waits for the NEXT animation to change it
        prevWeapon = weaponName;
    }
}

/////////////////////////////////////////////////////////
// T4 Teddy Bear Suppression
//
// We don't log teddy events (the score refund is captured implicitly
// via RD events). But we MUST still detect teddies to suppress false
// "box;take" loglines in WatchBoxOutcome's proximity fallback.
//
// T4's teddy path never sets chest_user, so WatchBoxOutcome falls
// into the proximity fallback. Without this suppression flag,
// it would log a false take for whatever weapon the animation
// last showed before the teddy.
/////////////////////////////////////////////////////////
WaitForBoxTeddySuppression()
{
    for ( ;; )
    {
        level waittill( "weapon_fly_away_start" );

        chests = getEntArray( "treasure_chest_use", "targetname" );
        if ( !IsDefined( chests ) )
        {
            continue;
        }

        // Mark ALL chests so WatchBoxOutcome skips its proximity fallback
        for ( k = 0; k < chests.size; k++ )
        {
            chests[k]._zm_stats_teddy = true;
        }
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
