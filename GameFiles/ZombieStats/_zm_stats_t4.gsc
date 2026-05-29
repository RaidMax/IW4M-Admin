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
    // Box detection: all four stock T4 maps (Nacht/Verrückt/Shi No Numa/
    // Der Riese) plus Der-Riese-derived customs. No map-name check —
    // outcome resolution polls user_grabbed_weapon notify + self.timedOut
    // + iw4m_box_teddy_marker, all of which exist uniformly across the
    // stock maps. See the header comment above WaitForMysteryBox.
    thread WaitForMysteryBox();
    thread WaitForBoxTeddySuppression();
    thread WaitForTrapActivations();
    thread WaitForEasterEggComplete();
    thread WaitForEasterEggSteps();
    thread WatchPowerSwitches();
    thread WatchPowerStateChanges();
    thread WatchZombiesRemaining();

    // --- Zombie Event Log Format --- //
    // Combat events (legacy format):
    //   AK, AD, K, D = kills/damage (unchanged)
    //   RD, RC = round data/complete (unchanged)
    //
    // Unified ZP/ZW format:
    //   ZP;{player};down                                    = player downed
    //   ZP;{player};revive;{reviver}                        = player co-op revived
    //   ZP;{player};revive;self                             = self-revive (T5+; T4 never emits — no self-revive mechanic in W@W)
    //   ZP;{player};perk;buy;{perkName};{cost}              = perk purchased (weapon-switch poll — T4 has no perk_bought notify)
    //   ZP;{player};powerup;grab;{powerupName}              = powerup grabbed
    //   ZP;{player};weapon;buy;{weaponName};{cost}          = wall weapon purchase
    //   ZP;{player};weapon;upgrade;{old};{new};{cost}       = pack-a-punch
    //   ZP;{player};weapon;abandon;{weapon};{cost}          = pap timeout
    //   ZP;{player};box;take;{weaponName};{cost}            = box weapon taken
    //   ZP;{player};box;pass;{weaponName};{cost}            = box weapon passed
    //   ZP;{player};box;teddy;{cost}                        = teddy bear (box moves)
    //   ZP;{player};door;buy;{cost}                         = door/debris opened
    //   ZP;{player};trap;activate;{trapType};{cost}         = trap activated
    //   ZW;round_special;{round};{type}                     = dog round (T4 only has dog_round flag)
    //   ZW;zombies;{round};{remaining};{alive}              = engine zombies-left poll
    //   ZW;power;on;world|player;[info]                     = power activated (T4 never powers off)
    //   ZW;easter_egg;step;{key}                            = EE progress
    //   ZW;easter_egg;complete;{map}                        = EE finalised

    SetupCallbacks();
    thread WatchdogCallbacks();
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

    // down/revive events
    level.callbackPlayerLastStandOriginal = level.callbackPlayerLastStand;
    level.callbackPlayerLastStand = ::OnPlayerDowned;

    // Powerups are observed via proximity polling in WaitForPowerupSpawned
    // / WaitForPowerupGrab, NOT by hooking level.zombie_powerup_grab_func.
    // Hooking that callback replaces the engine's own grab function, which
    // stops the powerup effect from being applied (Max Ammo wouldn't refill,
    // Insta-Kill wouldn't trigger, etc.).
}

/////////////////////////////////////////////////////////
// Re-installs our combat-event hooks if a map script
// overwrites them post-init. Stock T4 maps don't do this,
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
        
        LogPrint( "GSE;ZP;" + BuildPlayerInfoString( self ) + ";revive;" + BuildPlayerInfoString( reviver ) + "\n" );
    }
}

/////////////////////////////////////////////////////////
// Waits until player changes weapons and checks to see
// if weapon is a perk weapon. Prints to gamelog if true.
//
// Engine limitation: T4 has NO perk_bought notify and NO
// level.perk_bought_func hookpoint. vending_trigger_think
// in _zombiemode_perks.gsc grants the perk via SetPerk()
// + increments player.stats["perks"], but never emits any
// player notify we could subscribe to. Weapon-switch poll
// is therefore the only detection path on T4. Edge case:
// if a player buys a perk and is downed/killed before the
// next 0.1s tick, the perk-bought emission is dropped.
// T5/T6/T7 all moved to notify-driven detection.
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
                LogPrint( "GSE;ZP;" + BuildPlayerInfoString( self ) + ";perk;buy;" + currentWeapon + ";0\n" );
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

                LogPrint( "GSE;ZP;" + BuildPlayerInfoString( players[i] ) + ";powerup;grab;" + powerup + "\n" );

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

        // Detect special-round type for the round about to begin and emit a
        // GSE;ZW;round_special;<round>;<type> line so IW4MAdmin can flag the round in the
        // breakdown UI and skip Seconds-Per-Horde for round types where the
        // static budget formula doesn't apply (dogs replace the regular
        // zombie spawn budget; SPH would render visibly wrong otherwise).
        // T4 only has dog rounds (Shi No Numa / Der Riese hellhounds);
        // flag_exists guards keep us safe on maps that never initialised
        // the flag (e.g. Nacht der Untoten, Verruckt).
        EmitSpecialRoundIfAny();
    }
}

EmitSpecialRoundIfAny()
{
    // T4 doesn't expose flag_exists() — guard with IsDefined on the underlying
    // level.flag dict entry (portable across T4/T5/T6 and avoids T4's flag()
    // assertEx on uninitialised flag names).
    specialType = "";
    if ( IsDefined( level.flag ) && IsDefined( level.flag[ "dog_round" ] ) && level.flag[ "dog_round" ] )
    {
        specialType = "dog";
    }

    if ( specialType != "" )
    {
        logPrint( "GSE;ZW;round_special;" + level.round_number + ";" + specialType + "\n" );
    }
}

//-------------------//
//---- Callbacks ----//
//-------------------//

// See _zm_stats_t6.gsc for the full rationale. A zombie with N health can only take
// N damage; the engine can hand grossly inflated iDamage (splash / environmental, and
// on some special enemies an overflowed ~2^31), so bound by the victim's HP. Falls back
// to level.zombie_health when maxhealth is undefined (special enemies) — that was the
// case leaking past the old maxhealth-only guard.
GetReportedDamageCap()
{
    if ( IsDefined( self.maxhealth ) && self.maxhealth > 0 )
    {
        return self.maxhealth;
    }
    if ( IsDefined( level.zombie_health ) && level.zombie_health > 0 )
    {
        return level.zombie_health;
    }
    return undefined;
}

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
            reportedDamage = iDamage;
            cap = self GetReportedDamageCap();
            if ( IsDefined( cap ) && reportedDamage > cap )
            {
                reportedDamage = cap;
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
        // include overkill / engine-inflated iDamage. Same fallback as OnActorDamage.
        damage = iDamage;
        cap = self GetReportedDamageCap();
        if ( IsDefined( cap ) && damage > cap )
        {
            damage = cap;
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

        // Player victim — bound received damage by the player's max HP. self is the player.
        reportedDamage = iDamage;
        cap = self GetReportedDamageCap();
        if ( IsDefined( cap ) && reportedDamage > cap )
        {
            reportedDamage = cap;
        }

        LogPrint( "GSE;D;" + victimInfo + ";" + attackerInfo + ";" + sWeapon + ";" + reportedDamage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
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

    LogPrint( "GSE;ZP;" + BuildPlayerInfoString( self ) + ";down\n" );

    [[level.callbackPlayerLastStandOriginal]]( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration );
}

//-----------------//
//---- Helpers ----//
//-----------------//

PrintPlayerRoundData( isGameOver )
{
    // Skip emission entirely if level.round_number is undefined — fires during
    // post-game-over shutdown / exit_level cleanup with no round context. Without
    // this guard, currentRound defaults to 1 and emits a spurious GSE;RC;1 between
    // the legit final RC and ExitLevel (see match 1614).
    if ( !IsDefined( level.round_number ) )
    {
        return;
    }

    players = get_players();
    currentRound = level.round_number;

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

        LogPrint( "GSE;ZP;" + BuildPlayerInfoString( player ) + ";weapon;buy;" + weaponName + ";" + cost + "\n" );
    }
}

/////////////////////////////////////////////////////////
// T4 Pack-a-Punch detection (Der Riese + Der-Riese-derived custom maps).
//
// Engine reference: `_zombiemode_perks.gsc::vending_upgrade()` per
// "zombie_vending_upgrade" trigger entity. Key state/notifies:
//   - self waittill("trigger", player)        — BUY press
//   - self.current_weapon = currentWeapon     — set after engine accepts buy
//                                                (gates: score>=5000, weapon
//                                                in level.zombie_include_weapons
//                                                with "_upgraded" variant,
//                                                !laststand, !throwing,
//                                                !switching)
//   - ~3.5s upgrade animation
//   - self notify("pap_taken")                — engine fires when buyer grabs
//   - self notify("pap_timeout")              — engine fires after ~15s no grab
//   - self.current_weapon = ""                — iter ends
//
// Detection mirrors the box-detection design:
//   - WatchPapTakenFlag / WatchPapTimeoutFlag — bridge engine notifies to
//     per-iter flags for outcome resolution
//   - WatchPapTriggerForBuyer                  — lock-first buyer capture with
//     affordability + upgradeable gates client-side replicating engine's own
//     checks. VerifyPapBuyerLock unlocks 0.25s later if engine didn't accept
//     (handles the laststand/throwing/switching gates we don't replicate).
//   - WatchPapOutcome                          — iter loop. Phase 1 polls
//     current_weapon empty→non-empty (engine accepted a buy). Phase 2 polls
//     non-empty→empty (iter ended). Resolves outcome from flag state with
//     buyer-weapon match check, emits the appropriate event.
//
// Emits:
//   - weapon;upgrade;{old};{new};5000  — pap_taken (player took the upgrade)
//   - weapon;abandon;{weapon};5000     — pap_timeout (player walked away)
//
// Per-trigger state:
//   - self.iw4m_pap_taken_flag        — set by WatchPapTakenFlag, reset per iter
//   - self.iw4m_pap_timeout_flag      — set by WatchPapTimeoutFlag, reset per iter
//   - self.iw4m_pap_buyer             — locked by WatchPapTriggerForBuyer,
//                                        unlocked by VerifyPapBuyerLock if rejected,
//                                        reset per iter at Phase 1 entry
//   - self.iw4m_pap_buyer_weapon      — captured at lock time for the match check
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
        triggers[i].iw4m_pap_disconnect_flag = false;

        triggers[i] thread WatchPapOutcome();
        triggers[i] thread WatchPapTriggerForBuyer();
        triggers[i] thread WatchPapTakenFlag();
        triggers[i] thread WatchPapTimeoutFlag();
        triggers[i] thread WatchPapDisconnectFlag();
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
        if ( self.iw4m_pap_taken_flag || self.iw4m_pap_timeout_flag || self.iw4m_pap_disconnect_flag )
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
        if ( self.iw4m_pap_taken_flag || self.iw4m_pap_timeout_flag || self.iw4m_pap_disconnect_flag )
        {
            continue;
        }
        self.iw4m_pap_timeout_flag = true;
    }
}

// T4 engine does NOT emit pap_player_disconnected (T6/T7's _zm_perks.gsc does;
// T4's _zombiemode_perks.gsc has no equivalent). We synthesise it via the
// WatchPapBuyerDisconnect helper threaded at lock time — watches the buyer's
// engine "disconnect" notify and re-emits as "pap_player_disconnected" on the
// trigger. Lets the rest of the pattern stay aligned with T6/T7 verbatim.
WatchPapDisconnectFlag()
{
    for ( ;; )
    {
        self waittill( "pap_player_disconnected" );
        if ( self.iw4m_pap_taken_flag || self.iw4m_pap_timeout_flag || self.iw4m_pap_disconnect_flag )
        {
            continue;
        }
        self.iw4m_pap_disconnect_flag = true;
    }
}

WatchPapBuyerDisconnect()
{
    self endon( "pap_taken" );
    self endon( "pap_timeout" );

    buyer = self.iw4m_pap_buyer;
    if ( !IsDefined( buyer ) || !IsPlayer( buyer ) )
    {
        return;
    }

    buyer waittill( "disconnect" );
    self notify( "pap_player_disconnected" );
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
            self thread WatchPapBuyerDisconnect();
            continue;
        }

        // Phase1 path: engine hasn't accepted yet. Replicate engine gates
        // (score, upgradeable weapon) so we don't lock on rejected presses.
        if ( !IsDefined( who.score ) || who.score < 5000 )
        {
            continue;
        }

        // T4 weapon naming: bare name with "_upgraded" variant in
        // level.zombie_include_weapons (no _zm suffix as in T5/T6).
        weapon = who GetCurrentWeapon();
        if ( weapon == "" || weapon == "none" )
        {
            continue;
        }
        if ( !IsDefined( level.zombie_include_weapons ) || !IsDefined( level.zombie_include_weapons[weapon] ) )
        {
            continue;
        }
        if ( !IsDefined( level.zombie_include_weapons[weapon + "_upgraded"] ) )
        {
            continue;
        }

        self.iw4m_pap_buyer = who;
        self.iw4m_pap_buyer_weapon = weapon;
        self thread WatchPapBuyerDisconnect();

        // Verify engine actually accepted within ~5 frames; unlock otherwise.
        // Handles engine-side gates we don't replicate (laststand, throwing
        // grenade, switching weapons).
        self thread VerifyPapBuyerLock();
    }
}

VerifyPapBuyerLock()
{
    // Poll for engine acceptance up to 2s. Single 0.25s wait was too short on
    // T6 (engine sometimes delays setting self.current_weapon past 0.25s,
    // unlocking legit buyer → later stale F-press re-locks wrong weapon →
    // false mismatch → skipped emit). Polling fix is identical across T4/T5/T6
    // even though only T6 was observed failing — defensive consistency.
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
        self.iw4m_pap_disconnect_flag = false;
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

        // Disconnect short-circuits emission (no player to credit).
        if ( self.iw4m_pap_disconnect_flag )
        {
            continue;
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

        if ( isTaken )
        {
            newWeapon = oldWeapon + "_upgraded";
            LogPrint( "GSE;ZP;" + BuildPlayerInfoString( self.iw4m_pap_buyer ) + ";weapon;upgrade;" + oldWeapon + ";" + newWeapon + ";5000\n" );
        }
        else if ( isTimeout )
        {
            LogPrint( "GSE;ZP;" + BuildPlayerInfoString( self.iw4m_pap_buyer ) + ";weapon;abandon;" + oldWeapon + ";5000\n" );
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

    LogPrint( "GSE;ZP;" + BuildPlayerInfoString( player ) + ";door;buy;" + cost + "\n" );
}

/////////////////////////////////////////////////////////
// T4 Mystery Box Detection (all stock T4 maps + Der-Riese-derived customs).
//
// Engine reference: `_zombiemode_weapons.gsc` per map. Key entities/state:
//   - Chest trigger: targetname "treasure_chest_use" (all stock T4 maps)
//   - Entity chain: chest → lid (self.target) → weapon_spawn_org (lid.target)
//   - weapon_spawn_org.weapon_string: cycles ~40 times during the ~3.9s
//     randomization animation; final value persists until the next pull.
//   - self.chest_user: assigned in treasure_chest_think on Der-Riese-style
//     chests only (Nacht/Verrückt/Shi No Numa don't set it). NOT relied on
//     for outcome detection — set+cleared inside one cooperative-scheduler
//     frame on instant grabs even on Der Riese, so polling can't see it.
//   - self.timedOut: false at the top of every treasure_chest_think iter on
//     all four stock maps, true after the 12s no-grab timeout. Used here as
//     both the per-chest "is supported chest" discriminator (no map-name
//     check) AND the pass outcome signal.
//   - self notify("user_grabbed_weapon"): fires immediately before the
//     weapon is given on all four stock maps + Der-Riese-derived customs
//     (Nacht L323, Verrückt L447, Shi No L535, Der Riese L820). Primary
//     take outcome signal — captured by WatchUserGrabbedFlag into
//     self.iw4m_box_user_grabbed.
//   - level notify("weapon_fly_away_start"): fires on the teddy-bear path
//     (Verrückt + Shi No + Der Riese; Nacht has no teddy). Captured by
//     WaitForBoxTeddySuppression into self.iw4m_box_teddy_marker, scoped
//     to chests in late_phase so the mark can't bleed across iterations.
//   - "trigger" notify on the chest entity: fires whenever a player presses
//     USE on the chest. Recorded as self.iw4m_box_last_trigger and used
//     as the buyer-attribution fallback when chest_user wasn't captured
//     (always the case on Nacht/Verrückt/Shi No and on teddy paths).
//
// Outcome resolution (WatchBoxOutcome):
//   Phase 3 polls three independent flags every 0.1s with a 25s hard cap.
//   First-to-fire wins; priority is teddy > take > pass for the rare case
//   that more than one is set. If no flag fires we skip emission rather
//   than guess. Buyer comes from chest_user when available (Der Riese
//   patient grabs), else from the trigger fallback.
//
// Per-chest state used by this subsystem:
//   - self.iw4m_box_user_grabbed    — set by WatchUserGrabbedFlag, reset per iter
//   - self.iw4m_box_teddy_marker    — set by suppression, consumed in Phase 3
//   - self.iw4m_box_in_late_phase   — true between Phase 1 exit and iter end
//   - self.iw4m_box_last_trigger    — most recent player who pressed USE on
//                                     this chest; reset at iter end
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
        chests[i].iw4m_box_user_grabbed = false;

        // Pre-cache weapon_spawn_org via lid traversal so WatchBoxOutcome
        // can read weapon_string without re-traversing every poll.
        if ( IsDefined( chests[i].target ) )
        {
            lid = getent( chests[i].target, "targetname" );
            if ( IsDefined( lid ) && IsDefined( lid.target ) )
            {
                weaponSpawnOrg = getent( lid.target, "targetname" );
                if ( IsDefined( weaponSpawnOrg ) )
                {
                    chests[i].iw4m_weapon_spawn_org = weaponSpawnOrg;
                }
            }
        }

        chests[i] thread WatchBoxOutcome();
        chests[i] thread WatchBoxTriggerForBuyer();
        chests[i] thread WatchUserGrabbedFlag();
    }
}

/////////////////////////////////////////////////////////
// Sets a per-chest "take" flag on the engine's user_grabbed_weapon
// notify. Fires on all four stock T4 maps (Nacht L323, Verrückt L447,
// Shi No L535, Der Riese L820), inside treasure_chest_think on `self`
// (the chest trigger) immediately before treasure_chest_give_weapon.
//
// Primary outcome signal for take detection — chest_user is unreliable
// across the maps that don't assign it (Nacht/Verrückt/Shi No), and even
// on Der Riese it gets set+cleared inside one frame on instant grabs.
// WatchBoxOutcome polls this flag (plus self.timedOut for pass and
// iw4m_box_teddy_marker for teddy) so we work uniformly across all maps.
/////////////////////////////////////////////////////////
WatchUserGrabbedFlag()
{
    for ( ;; )
    {
        self waittill( "user_grabbed_weapon" );
        self.iw4m_box_user_grabbed = true;
    }
}

/////////////////////////////////////////////////////////
// Teddy bear suppression.
//
// Engine fires `level notify("weapon_fly_away_start")` on the teddy path
// (Verrückt + Shi No + Der Riese; Nacht has no teddy). We mark only chests
// in `iw4m_box_in_late_phase == true` so an idle chest that hasn't entered
// Phase 1 since the previous teddy can't carry a stale marker forward. On
// stock maps only one chest is active at a time, so exactly one chest gets
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
// recording the buyer on `self.iw4m_box_last_trigger`. Phase 3 uses this as
// the buyer source whenever chest_user wasn't captured (always the case on
// Nacht/Verrückt/Shi No, and on teddy paths everywhere).
//
// Lock-on-first-valid-press: we record only the FIRST trigger of an
// iteration that comes from a player who could afford the buy, then ignore
// every subsequent press until iter end (which clears the lock). This is
// the buyer because:
//   - The engine enforces `grabber == user` in treasure_chest_think on all
//     four stock T4 maps + Der-Riese-derived customs (Nacht L319, Verrückt
//     L443, Shi No L531, Der Riese L816). Other players' F presses fire
//     "trigger" but the engine discards them.
//   - The buyer's own grab press fires trigger again, but they're already
//     locked so it's a noop.
//   - Without the lock, in multiplayer, another player's noise press would
//     overwrite the real buyer mid-iter and we'd misattribute.
//
// Affordability gate: a player can press F with insufficient funds — trigger
// fires but the engine rejects. Without the score check we'd lock the wrong
// player and never update when the real buyer presses. Cost is read from
// level.zombie_treasure_chest_cost (Der Riese-style global, defaults to 950).
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
    if ( !IsDefined( self.iw4m_weapon_spawn_org ) )
    {
        return;
    }
    weaponSpawnOrg = self.iw4m_weapon_spawn_org;

    // Sentinel — never matches any real weapon name. Guards the first-iter
    // case where weapon_string is still undefined. On subsequent iters,
    // prevWeapon holds the previous iter's final weapon (stable until the
    // next animation begins).
    prevWeapon = "__none__";

    for ( ;; )
    {
        self.iw4m_box_user_grabbed = false;

        // Phase 1: wait for weapon_string to start changing (animation begins).
        animStarted = false;
        firstChanged = undefined;
        while ( !animStarted )
        {
            if ( IsDefined( weaponSpawnOrg.weapon_string ) )
            {
                if ( weaponSpawnOrg.weapon_string != prevWeapon )
                {
                    animStarted = true;
                    firstChanged = weaponSpawnOrg.weapon_string;
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

        self.iw4m_box_in_late_phase = true;

        // Phase 2: wait for weapon_string to stabilize (5 consecutive equal
        // polls × 0.1s = ~0.5s of stability). Also opportunistically capture
        // chest_user — present on Der Riese patient grabs, missed on instant
        // grabs and absent entirely on Nacht/Verrückt/Shi No.
        stableFrames = 0;
        stableWeapon = firstChanged;
        capturedUser = undefined;
        while ( stableFrames < 5 )
        {
            wait ( 0.1 );

            currentWeapon = undefined;
            if ( IsDefined( weaponSpawnOrg.weapon_string ) )
            {
                currentWeapon = weaponSpawnOrg.weapon_string;
            }

            if ( IsDefined( currentWeapon ) && currentWeapon == stableWeapon )
            {
                stableFrames = stableFrames + 1;
            }
            else
            {
                stableFrames = 0;
                if ( IsDefined( currentWeapon ) )
                {
                    stableWeapon = currentWeapon;
                }
            }

            if ( !IsDefined( capturedUser ) && IsDefined( self.chest_user ) && IsPlayer( self.chest_user ) )
            {
                capturedUser = self.chest_user;
            }
        }

        weaponName = stableWeapon;
        if ( !IsDefined( weaponName ) )
        {
            weaponName = "undef";
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

        // timedOutDefined is the per-chest "is supported chest" gate — only
        // chests whose treasure_chest_think initialises self.timedOut emit
        // outcomes. All four stock maps qualify; arbitrary custom chest
        // implementations that don't init timedOut silently no-op.
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

        // Phase 3: wait for one of three independent outcome signals — take
        // (user_grabbed_weapon notify → iw4m_box_user_grabbed), pass (engine
        // sets self.timedOut after 12s), or teddy (weapon_fly_away_start →
        // iw4m_box_teddy_marker). 25s hard cap covers 12s timeout + grab
        // animation + safety; hitting the cap means no signal fired and we
        // skip emission rather than guessing.
        closeWaits = 0;
        maxWaits = 250;
        for ( ;; )
        {
            if ( self.iw4m_box_user_grabbed )
            {
                break;
            }
            if ( IsDefined( self.timedOut ) && self.timedOut )
            {
                break;
            }
            if ( IsDefined( self.iw4m_box_teddy_marker ) && self.iw4m_box_teddy_marker )
            {
                break;
            }
            if ( closeWaits >= maxWaits )
            {
                break;
            }
            wait ( 0.1 );
            closeWaits = closeWaits + 1;
        }

        isTake = false;
        if ( self.iw4m_box_user_grabbed )
        {
            isTake = true;
        }
        isPass = false;
        if ( IsDefined( self.timedOut ) && self.timedOut )
        {
            isPass = true;
        }
        isTeddy = false;
        if ( IsDefined( self.iw4m_box_teddy_marker ) && self.iw4m_box_teddy_marker )
        {
            isTeddy = true;
        }
        self.iw4m_box_teddy_marker = false;

        // Buyer fallback — typical on Nacht/Verrückt/Shi No (no chest_user)
        // and on teddy paths everywhere (chest_user never set on teddy).
        // Gated on timedOutDefined so we don't emit for chests we don't
        // understand the iteration boundaries of.
        if ( !IsDefined( user ) && timedOutDefined == 1
             && IsDefined( self.iw4m_box_last_trigger ) && IsPlayer( self.iw4m_box_last_trigger ) )
        {
            user = self.iw4m_box_last_trigger;
        }

        // Outcome priority: teddy > take > pass. Mutually exclusive in
        // practice; priority is defensive against engine weirdness.
        if ( IsDefined( user ) )
        {
            if ( isTeddy )
            {
                LogPrint( "GSE;ZP;" + BuildPlayerInfoString( user ) + ";box;teddy;" + cost + "\n" );
            }
            else if ( isTake )
            {
                LogPrint( "GSE;ZP;" + BuildPlayerInfoString( user ) + ";box;take;" + weaponName + ";" + cost + "\n" );
            }
            else if ( isPass )
            {
                LogPrint( "GSE;ZP;" + BuildPlayerInfoString( user ) + ";box;pass;" + weaponName + ";" + cost + "\n" );
            }
        }

        self.iw4m_box_last_trigger = undefined;
        self.iw4m_box_in_late_phase = false;
        self.iw4m_box_user_grabbed = false;

        // Seed prevWeapon so next iter's Phase 1 waits for the NEXT animation.
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

        LogPrint( "GSE;ZP;" + BuildPlayerInfoString( player ) + ";trap;activate;electric;" + cost + "\n" );
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

/////////////////////////////////////////////////////////
// Easter Egg main quest detection.
//
// T4 has only one map with a tracked EE — Der Riese's "Fly Trap"
// (achievement DLC3_ZOMBIE_ANTI_GRAVITY at nazi_zombie_factory.gsc:1530).
// The engine fires achievement_notify directly with no `level notify`.
//
// Phases:
//   1. Player shoots fly trap control panel with an upgraded weapon
//      → flag_set("hide_and_seek")  [phase START — NOT completion]
//   2. Player must then shoot all 3 hidden targets
//      (ee_exp_monkey, ee_bowie_bear, ee_perk_bear). Each shot increments
//      level.flytrap_counter. When counter == 3, EE is complete.
//
// We poll level.flytrap_counter rather than flag_wait on the per-target
// flags because those flags are flag_init'd inside hide_and_seek_target()
// only after flytrap() runs — flag_wait on an uninitialized flag asserts.
// Polling an integer is safe regardless of init order.
//
// Other T4 maps (Nacht, Verruckt, Shi No Numa) have no main EE → no-op.
/////////////////////////////////////////////////////////
WaitForEasterEggComplete()
{
    level endon( "end_game" );

    if ( level.script != "nazi_zombie_factory" )
    {
        logprint( "[ZM-EE] No EE watcher configured for map=" + level.script + "\n" );
        return;
    }

    logprint( "[ZM-EE] Watcher armed: map=" + level.script + " counter=level.flytrap_counter target=3\n" );

    // Step 1: panel-shot phase-start. Stock script flag_set("hide_and_seek") right
    // after the panel is shot with an upgraded weapon. The watcher polls IsDefined
    // before calling flag_wait, since stock flag_init runs inside flytrap() — not
    // main — so the flag isn't guaranteed to exist at level-init time.
    thread WatchDerRieseFlytrapPanel();

    // Steps 2-4: poll level.flytrap_counter and emit a step event each time it
    // ticks up. Engine is single-threaded so a ticked-past value would be missed
    // if we slept too long between checks; 0.5s easily covers human reaction time.
    prev = 0;
    while ( !IsDefined( level.flytrap_counter ) || level.flytrap_counter < 3 )
    {
        if ( IsDefined( level.flytrap_counter ) && level.flytrap_counter > prev )
        {
            // Fire one step event per integer tick (covers the rare case the
            // counter jumps by >1, though stock script increments by exactly 1).
            for ( i = prev + 1; i <= level.flytrap_counter && i <= 3; i++ )
            {
                stepKey = "t4_dr_flytrap_target_" + i;
                logprint( "[ZM-EE] Step fired: " + stepKey + "\n" );
                logprint( "GSE;ZW;easter_egg;step;" + stepKey + "\n" );
            }
            prev = level.flytrap_counter;
        }
        wait ( 0.5 );
    }

    // The 2→3 tick exits the while early so the final step never enters the loop
    // body. Flush any unobserved steps now (covers prev=2→3, plus pathological
    // multi-tick cases where counter jumped >1 across the exit boundary).
    for ( i = prev + 1; i <= 3; i++ )
    {
        stepKey = "t4_dr_flytrap_target_" + i;
        logprint( "[ZM-EE] Step fired: " + stepKey + "\n" );
        logprint( "GSE;ZW;easter_egg;step;" + stepKey + "\n" );
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
    logprint( "GSE;ZW;easter_egg;complete;" + level.script + "\n" );
}

// Panel-shot phase-start. Stock factory.gsc flag_set("hide_and_seek") inside
// flytrap() once the upgraded-weapon shot lands; block on the flag rather than
// hooking the entity directly since the panel target ent isn't named consistently.
WatchDerRieseFlytrapPanel()
{
    level endon( "end_game" );

    // Poll until flag_init has run. Stock flytrap() calls flag_init("hide_and_seek")
    // partway through level setup — calling flag_wait before that point would do
    // `!level.flag["hide_and_seek"]` on undefined and throw a runtime cast error.
    while ( !IsDefined( level.flag ) || !IsDefined( level.flag[ "hide_and_seek" ] ) )
    {
        wait ( 0.5 );
    }

    flag_wait( "hide_and_seek" );

    logprint( "[ZM-EE] Step fired: t4_dr_flytrap_panel\n" );
    logprint( "GSE;ZW;easter_egg;step;t4_dr_flytrap_panel\n" );
}

/////////////////////////////////////////////////////////
// Easter Egg STEP detection — song-egg progression.
//
// Each supported stock T4 map has ONE song EE, modelled as a single
// completion step. Der Riese is the exception: 3 distinct meteor stones
// to find, modelled as 3 steps.
//
//   • Verrückt (nazi_zombie_asylum): press E on `toilet` 3x. Stock
//     toilet_useage() sets level.eggs=1 + setmusicstate("eggs") on the
//     3rd trigger. Poll for level.eggs == 1.
//
//   • Shi No Numa (nazi_zombie_sumpf): press E on `toilet` (the phone)
//     4x — off-hook + dial 9-1-1. Stock sets level.eggs=1 + setmusic-
//     state("eggs") after the dial sequence. Poll for level.eggs == 1.
//
//   • Der Riese (nazi_zombie_factory): three meteor USE-triggers,
//     unchanged from prior implementation.
//
//   • Nacht der Untoten (nazi_zombie_prototype): NOT supported. Stock's
//     kzmb radio damage path is broken on Pluto T4 dedicated servers —
//     the entity exists but never receives the "damage" notify, so no
//     reliable hook is available. Verified empirically; map omitted.
//
// All paths gate on first-player-connect: Pluto T4 dedicated servers
// pause level-time when no players are connected, so 3-second waits
// effectively block forever on an empty lobby and stock entity init
// may complete after our level-init thread starts.
//
// Custom maps that don't use these entity names silently no-op.
/////////////////////////////////////////////////////////
WaitForEasterEggSteps()
{
    level endon( "end_game" );

    // Pluto T4 dedi: level-time only ticks with players connected. Block until
    // the first one shows up so subsequent waits / getent calls behave normally.
    while ( getplayers().size == 0 )
    {
        wait ( 1 );
    }

    switch ( level.script )
    {
        case "nazi_zombie_asylum":    HookEggsFlagSong( "t4_vr_song" ); return;
        case "nazi_zombie_sumpf":     HookEggsFlagSong( "t4_sh_song" ); return;
        case "nazi_zombie_factory":   HookDerRieseMeteors();            return;
        default:
            logprint( "[ZM-EE] No step watcher configured for map=" + level.script + "\n" );
            return;
    }
}

// Verrückt + Shi No Numa — both stock GSCs set level.eggs=1 +
// setmusicstate("eggs") at the moment the song trigger fires (after the 3rd
// toilet press on Verrückt, after the dial sequence on Shi No). Polling
// level.eggs is map-agnostic and survives any GSC trigger-sequence quirks.
HookEggsFlagSong( stepKey )
{
    level endon( "end_game" );

    logprint( "[ZM-EE] Step watcher armed: " + stepKey + " (polling level.eggs)\n" );

    // 0.5s matches the cadence used by WatchDerRieseFlytrapPanel/flytrap_counter
    // — well within human reaction time, low CPU cost.
    while ( !IsDefined( level.eggs ) || level.eggs != 1 )
    {
        wait ( 0.5 );
    }

    logprint( "[ZM-EE] Step fired: " + stepKey + "\n" );
    logprint( "GSE;ZW;easter_egg;step;" + stepKey + "\n" );
}

// Use-trigger path (Der Riese — meteor stones, press E to interact).
// Stock nazi_zombie_factory::main spawns three meteor_egg() threads on
// targetnames meteor_one / meteor_two / meteor_three, each emitting
// "trigger" exactly once when used.
HookDerRieseMeteors()
{
    level endon( "end_game" );

    targetNames = [];
    targetNames[0] = "meteor_one";
    targetNames[1] = "meteor_two";
    targetNames[2] = "meteor_three";

    // Per-target poll because stock factory::main runs the meteor_egg setup
    // alongside other level scripts; entity availability isn't strictly ordered.
    for ( i = 0; i < targetNames.size; i++ )
    {
        thread HookOneDerRieseMeteor( targetNames[i], i + 1 );
    }
}

HookOneDerRieseMeteor( targetName, oneBasedIndex )
{
    level endon( "end_game" );

    trig = undefined;
    for ( attempt = 0; attempt < 30; attempt++ )
    {
        trig = getent( targetName, "targetname" );
        if ( IsDefined( trig ) )
        {
            break;
        }
        wait ( 1 );
    }

    if ( !IsDefined( trig ) )
    {
        logprint( "[ZM-EE] Der Riese meteor not found: " + targetName + " (custom map?)\n" );
        return;
    }

    stepKey = "t4_dr_meteor_" + oneBasedIndex;
    logprint( "[ZM-EE] Step watcher armed: " + stepKey + " target=" + targetName + "\n" );

    // Stock script ALSO hooks waittill("trigger", player) on this entity to
    // increment level.meteor_counter. waittill is broadcast — both threads
    // receive the notify, so we don't interfere with the stock counter logic.
    trig waittill( "trigger" );

    logprint( "[ZM-EE] Step fired: " + stepKey + "\n" );
    logprint( "GSE;ZW;easter_egg;step;" + stepKey + "\n" );
}

/////////////////////////////////////////////////////////
// PWR — Map power state monitoring (T4)
/////////////////////////////////////////////////////////
//
// T4 has no unified "power_on" flag (the T5/T6 _zm.gsc system doesn't exist
// here). Detection instead piggybacks on the per-perk activation notifies
// fired from each perk machine when power comes online — every T4 zombie
// map with power (Verrückt, Shi No Numa, Der Riese) fires
// "specialty_quickrevive_power_on" since QR is universal across them. Nacht
// has no perks at all, so the notify never fires there — correct behaviour.
//
// One-shot dedup via a level flag — multiple perk notifies fire in sequence
// when the master switch is hit; we want a single power_on event per match.
// T4 has no power-off mechanic; only the on side is implemented.
//
// Player attribution mirrors T5/T6: parallel use-trigger watcher records the
// activator on level for the most recent ~5s, applied to the power-on emit
// if recent. Falls back to world otherwise.
/////////////////////////////////////////////////////////

WatchPowerSwitches()
{
    level endon( "end_game" );

    wait ( 2 );

    candidates = [];
    candidates[0] = "use_power_switch";
    candidates[1] = "power_switch_trig";
    candidates[2] = "power_button";

    triggers = [];
    for ( c = 0; c < candidates.size; c++ )
    {
        ents = getentarray( candidates[c], "targetname" );
        for ( i = 0; i < ents.size; i++ )
        {
            triggers[triggers.size] = ents[i];
        }
    }

    for ( t = 0; t < triggers.size; t++ )
    {
        triggers[t] thread WatchSinglePowerSwitch();
    }
}

WatchSinglePowerSwitch()
{
    level endon( "end_game" );
    self endon( "death" );

    for ( ;; )
    {
        self waittill( "trigger", who );
        if ( IsPlayer( who ) )
        {
            level._iw4m_power_activator = who;
            level._iw4m_power_activator_time = gettime();
        }
    }
}

WatchPowerStateChanges()
{
    level endon( "end_game" );

    // Wait for the canonical T4 power-on signal — fires on every map that
    // has a power switch + perk machines (Verrückt, Shi No Numa, Der Riese).
    // Nacht has no perks → notify never fires → no event ever emitted.
    level waittill( "specialty_quickrevive_power_on" );

    if ( IsDefined( level._iw4m_power_emitted ) && level._iw4m_power_emitted )
    {
        return;
    }
    level._iw4m_power_emitted = true;

    activator = undefined;
    if ( IsDefined( level._iw4m_power_activator ) && IsDefined( level._iw4m_power_activator_time ) )
    {
        if ( gettime() - level._iw4m_power_activator_time < 5000 )
        {
            activator = level._iw4m_power_activator;
        }
    }

    if ( IsDefined( activator ) )
    {
        logPrint( "GSE;ZW;power;on;player;" + BuildPlayerInfoString( activator ) + "\n" );
    }
    else
    {
        logPrint( "GSE;ZW;power;on;world\n" );
    }
}

// Periodic emission of (round, zombies-remaining-to-spawn, currently-alive). Lets
// the live modal compute true "zombies cleared" = budget - remaining - alive,
// which captures trap kills / environmental kills / friendly grenade splash —
// all things that don't credit to a player's kill count but still reduce the
// round's spawn pool. Without this, live SPH would systematically over-estimate
// pace on trap-heavy strategies (Verruckt electric trap, Der Riese teleporters).
//
// Throttle: every 5s, only emits when remaining or alive changed since last
// emission. Matches the live-modal poll cadence — no point emitting faster
// than the UI refreshes. ~12 lines/min on an active round, none during
// intermission.
WatchZombiesRemaining()
{
    level endon( "end_game" );
    last_remaining = -1;
    last_alive = -1;
    while ( true )
    {
        wait( 5 );
        if ( !IsDefined( level.zombie_total ) || !IsDefined( level.round_number ) )
        {
            continue;
        }
        remaining = level.zombie_total;
        alive = get_enemy_count();
        if ( remaining == last_remaining && alive == last_alive )
        {
            continue;
        }
        logPrint( "GSE;ZW;zombies;" + level.round_number + ";" + remaining + ";" + alive + "\n" );
        last_remaining = remaining;
        last_alive = alive;
    }
}
