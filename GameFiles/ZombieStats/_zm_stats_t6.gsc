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
    // T6 box detection — notify-driven on self.zbarrier (T6's equivalent
    // of T5's chest_origin) with 3-tier user resolution + scoped teddy
    // suppression. See header comment above WaitForMysteryBox.
    thread WaitForMysteryBox();
    thread WaitForBoxTeddySuppression();
    thread WaitForTrapActivations();
    thread WaitForBuildables();
    thread WaitForCraftables();
    thread WaitForEasterEggComplete();
    thread WaitForT6EasterEggSteps();

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
/////////////////////////////////////////////////////////
// T6 Pack-a-Punch — DEBUG INSTRUMENTED BUILD
//
// Mirrors the T4 redesign (notify-driven, lock-first buyer attribution,
// distinct outcomes via pap_taken/pap_timeout). T6 adds a third notify:
// pap_player_disconnected (engine fires when buyer disconnects mid-iter).
// We track it via WatchPapDisconnectFlag and skip emission on disconnect
// (no player to credit).
//
// T6 engine reference (`_zm_perks.gsc::vending_weapon_upgrade`):
//   - Trigger discovery: targetname "zombie_vending" + script_noteworthy
//     "specialty_weapupgrade", OR legacy targetname "zombie_vending_upgrade"
//   - Field: self.current_weapon (~L630 set, ~L645 clear)
//   - Take notify: self notify("pap_taken")  ~L742
//   - Timeout notify: self notify("pap_timeout")  ~L793
//   - Disconnect notify: self notify("pap_player_disconnected")  ~L816
//   - Cost: 5000 base
//   - Timeout: level.packapunch_timeout = 15s
/////////////////////////////////////////////////////////
WaitForPackAPunch()
{
    wait ( 2 );

    // T6 PaP triggers come from two sources:
    //   1. targetname "zombie_vending" with script_noteworthy "specialty_weapupgrade"
    //   2. targetname "zombie_vending_upgrade" (legacy fallback)
    // Both get threaded with vending_weapon_upgrade() by the game.
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
        papTriggers[i].iw4m_pap_buyer = undefined;
        papTriggers[i].iw4m_pap_buyer_weapon = undefined;
        papTriggers[i].iw4m_pap_taken_flag = false;
        papTriggers[i].iw4m_pap_timeout_flag = false;
        papTriggers[i].iw4m_pap_disconnect_flag = false;

        papTriggers[i] thread WatchPapOutcome();
        papTriggers[i] thread WatchPapTriggerForBuyer();
        papTriggers[i] thread WatchPapTakenFlag();
        papTriggers[i] thread WatchPapTimeoutFlag();
        papTriggers[i] thread WatchPapDisconnectFlag();
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

        // T6 weapons have _zm suffix; upgrade variant is at
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

        // Verify engine actually accepted; unlock otherwise. Handles engine-
        // side gates we don't replicate (laststand, throwing grenade,
        // switching weapons).
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

        // Resolve actual cost engine charged. Engine sets self.cost (5000
        // base, 1000 during bonfire sale) and self.attachment_cost (2000
        // base, 1000 sale) on the trigger via vending_weapon_upgrade_cost.
        // Attachment-only upgrade fires on already-upgraded weapons on
        // re-PaP-enabled maps (BO2 attachment-perk maps); engine charges
        // attachment_cost in that case. Substring check on _upgraded is a
        // proxy for engine's will_upgrade_weapon_as_attachment gate (engine
        // also requires zombiemode_reusing_pack_a_punch + supports_attachments
        // — but those gates already passed if we're at phase1_enter with an
        // upgraded weapon). Misses pers_upgrade double_points modifier
        // (per-player premium feature, rare).
        cost = 5000;
        if ( IsDefined( self.cost ) ) { cost = self.cost; }
        if ( IsDefined( self.attachment_cost ) && IsSubStr( oldWeapon, "_upgraded" ) ) { cost = self.attachment_cost; }

        if ( self.iw4m_pap_taken_flag )
        {
            newWeapon = oldWeapon + "_upgraded";
            if ( IsDefined( level.zombie_weapons ) && IsDefined( level.zombie_weapons[oldWeapon] ) && IsDefined( level.zombie_weapons[oldWeapon].upgrade_name ) )
            {
                newWeapon = level.zombie_weapons[oldWeapon].upgrade_name;
            }
            logprint( "GSE;ZE;" + BuildPlayerInfoString( self.iw4m_pap_buyer ) + ";weapon;upgrade;" + oldWeapon + ";" + newWeapon + ";" + cost + "\n" );
        }
        else if ( self.iw4m_pap_timeout_flag )
        {
            logprint( "GSE;ZE;" + BuildPlayerInfoString( self.iw4m_pap_buyer ) + ";weapon;abandon;" + oldWeapon + ";" + cost + "\n" );
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
// T6 Mystery Box Detection (vanilla T6 + Tranzit/Buried/Origins/etc).
//
// Notify-driven on self.zbarrier — each engine box iteration emits
// exactly one randomization_done + one box_spin_done, giving 1:1
// mapping between our handler iters and engine pulls.
//
// Engine reference: `_zm_magicbox.gsc` from t6-scripts-main ZM/Core.
// Key entities/state:
//   - level.chests[]: array of all magic box trigger entities. (No
//     "treasure_chest_use" targetname lookup like T4/T5 — T6 publishes
//     the array directly.)
//   - self.zbarrier: per-chest, holds .weapon_string (cycles during
//     randomization, final value persists until cleared at L2227) and
//     emits randomization_done (L1204) / box_spin_done (L1291) /
//     weapon_grabbed notifies.
//   - self.chest_user: assigned BEFORE randomization (L454/461/468),
//     cleared at L628. Reliably observable at randomization_done resume
//     on the normal grab path.
//   - self.timedout (lowercase, matches engine L510/583). GSC field
//     access is case-insensitive but match the engine for clarity.
//   - self.grab_weapon_name: set at L525 from self.zbarrier.weapon_string
//     after randomization_done. Persists across iters until next pull.
//   - level "weapon_fly_away_start" notify (L1213): teddy path, ~0.5s
//     after randomization_done.
//
// 3-tier user resolution (same shape as T4/T5):
//   1. capturedUser snapshotted at randomization_done resume
//   2. live self.chest_user at box_spin_done resume
//   3. self.iw4m_box_last_trigger (parallel waittill ground truth)
//      — gated on IsDefined(self.timedout)
// Tier 3 is required for teddy attribution: treasure_chest_move
// (self.chest_user) is threaded at L521, then engine drops to L628
// (chest_user = undefined) without yielding — both tier 1 and tier 2
// miss. The trigger waittill captured the buyer earlier.
//
// Per-chest state:
//   - self.iw4m_box_teddy_marker      — set by suppression, consumed
//     at box_spin_done
//   - self.iw4m_box_in_late_phase     — true between rand_done and
//     iter end; suppression only marks chests with this flag
//   - self.iw4m_box_last_trigger      — parallel trigger capture,
//     cleared at iter end
//
// Note on Origins (zm_tomb): the custom `_zm_magicbox_tomb.gsc` only
// overrides visual/zbarrier-state machinery. Once a zone is captured,
// chests in that zone publish the same notifies as vanilla — no
// Origins-specific code needed here.
//
// Note on Buried: candy-lady mechanic dynamically prunes level.chests
// after init. Suppression iterating live level.chests handles this
// correctly — only chests in active rotation get marked.
/////////////////////////////////////////////////////////
WaitForMysteryBox()
{
    wait ( 5 );

    if ( !IsDefined( level.chests ) )
    {
        return;
    }

    for ( i = 0; i < level.chests.size; i++ )
    {
        level.chests[i].iw4m_box_teddy_marker = false;
        level.chests[i].iw4m_box_in_late_phase = false;

        level.chests[i] thread WatchBoxOutcome();
        level.chests[i] thread WatchBoxTriggerForBuyer();
    }
}

/////////////////////////////////////////////////////////
// Teddy bear suppression — same scoping rule as T4/T5: only mark
// chests flagged as iw4m_box_in_late_phase, otherwise level-scoped
// marking bleeds across iterations.
/////////////////////////////////////////////////////////
WaitForBoxTeddySuppression()
{
    wait ( 5 );
    for ( ;; )
    {
        level waittill( "weapon_fly_away_start" );

        if ( !IsDefined( level.chests ) )
        {
            continue;
        }

        for ( k = 0; k < level.chests.size; k++ )
        {
            if ( IsDefined( level.chests[k].iw4m_box_in_late_phase ) )
            {
                if ( level.chests[k].iw4m_box_in_late_phase )
                {
                    level.chests[k].iw4m_box_teddy_marker = true;
                }
            }
        }
    }
}

/////////////////////////////////////////////////////////
// Parallel ground-truth capture of who pressed USE on the chest.
// Used as tier-3 fallback for teddy attribution (chest_user is cleared
// in same frame as L521/L628 on teddy path) and instant-grab cases.
//
// Lock-on-first-valid-press: record only the FIRST trigger of an
// iteration that comes from a player who could afford the buy, then
// ignore every subsequent press until iter end clears the lock. This
// is the buyer because the engine enforces buyer-only-can-grab
// (treasure_chest_think rejects "trigger" from non-buyers). Without
// the lock, in multi-player F-spam, another player's noise press
// would overwrite the real buyer.
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
    if ( !IsDefined( self.zbarrier ) )
    {
        return;
    }

    for ( ;; )
    {
        // Iter starts when engine signals randomization done.
        self.zbarrier waittill( "randomization_done" );

        // Mark for teddy suppression scoping. Cleared at iter end.
        self.iw4m_box_in_late_phase = true;

        // Snapshot weapon + buyer immediately. weapon_string is
        // undefined on the teddy path. chest_user may also be
        // undefined on teddy because the engine doesn't yield
        // between L1204 and L628 — that's why tier-3 trigger
        // fallback is required for teddy attribution.
        weaponName = "undef";
        if ( IsDefined( self.zbarrier.weapon_string ) )
        {
            weaponName = self.zbarrier.weapon_string;
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
        self.zbarrier waittill( "box_spin_done" );

        // Safe to read final state — engine sleeps post-grab before
        // resetting timedout for the next iter.
        timedOutDefined = 0;
        timedOutValue = false;
        if ( IsDefined( self.timedout ) )
        {
            timedOutDefined = 1;
            if ( self.timedout )
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
        if ( IsDefined( self.zombie_cost ) )
        {
            cost = self.zombie_cost;
        }

        if ( IsDefined( user ) )
        {
            if ( isTeddy )
            {
                logprint( "GSE;ZE;" + BuildPlayerInfoString( user ) + ";box;teddy;" + cost + "\n" );
            }
            else if ( timedOutValue )
            {
                logprint( "GSE;ZE;" + BuildPlayerInfoString( user ) + ";box;pass;" + weaponName + ";" + cost + "\n" );
            }
            else
            {
                logprint( "GSE;ZE;" + BuildPlayerInfoString( user ) + ";box;take;" + weaponName + ";" + cost + "\n" );
            }
        }

        // Per-iter cleanup.
        self.iw4m_box_last_trigger = undefined;
        self.iw4m_box_in_late_phase = false;
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

/////////////////////////////////////////////////////////
// Monitors craftable completion via level notifies.
//
// Origins (zm_tomb) and Mob of the Dead (zm_prison) use the parallel
// _zm_craftables system instead of _zm_buildables, registering items
// in level.zombie_craftablestubs and emitting "<name>_crafted" on level
// (see _zm_craftables.gsc:1581 — `level notify( name + "_crafted", player )`).
//
// Origins items: 4 elemental staffs, zombie shield, dieseldrone (G-Strike),
// gramophone. MotD items: riot shield, packasplat (Acid Gat Kit), plane,
// refuelable_plane, quest_key1.
//
// We emit identical `GSE;ZE;...;build;complete;<name>` events so the
// downstream pipeline (BuildComplete event log, MapBuildableConfig) treats
// them uniformly. DISTINCT-by-name aggregation in the leaderboard service
// collapses any duplicate notifies (e.g. MotD's repeated refuelable_plane).
/////////////////////////////////////////////////////////
WaitForCraftables()
{
    wait ( 3 );

    if ( !IsDefined( level.zombie_craftablestubs ) )
    {
        return;
    }

    names = getArrayKeys( level.zombie_craftablestubs );

    for ( i = 0; i < names.size; i++ )
    {
        thread WatchCraftableComplete( names[i] );
    }

    // Origins-special: gramophone "fully crafted" requires all 6 vinyls placed
    // (player + master + 4 elemental records), which most matches never reach
    // even when the EE side is fully exercised. The community + EE-completion
    // semantics treat the gramophone as "built" the moment it's physically
    // placed on the music stand, which fires the gramophone_placed level flag
    // (zm_tomb_main_quest.gsc:286 — flag_set fires `level notify(flagname)`).
    // Fire BuildComplete on that signal in addition to the standard handler so
    // the buildables card actually reflects the player's progress.
    if ( IsDefined( level.script ) && level.script == "zm_tomb" )
    {
        thread WatchGramophonePlacement();
    }
}

WatchCraftableComplete( craftableName )
{
    for ( ;; )
    {
        level waittill( craftableName + "_crafted", player );

        if ( !IsDefined( player ) || !IsPlayer( player ) )
        {
            continue;
        }

        logprint( "GSE;ZE;" + BuildPlayerInfoString( player ) + ";build;complete;" + craftableName + "\n" );
    }
}

// Origins-only. The gramophone_placed flag fires whenever the player
// physically places the gramophone on a music stand (first or subsequent —
// it toggles on pickup/replace). We only emit on the FIRST set per match;
// downstream DISTINCT-by-name aggregation in the leaderboard service would
// dedupe duplicates anyway, but exiting after the first fire saves the
// per-cycle log noise. Player attribution falls to the first connected
// player since the flag notify carries no player arg — buildable lists are
// match-scoped (no per-player credit), so any valid player is fine.
WatchGramophonePlacement()
{
    level waittill( "gramophone_placed" );

    players = getPlayers();
    if ( !IsDefined( players ) || players.size == 0 )
    {
        return;
    }

    logprint( "GSE;ZE;" + BuildPlayerInfoString( players[0] ) + ";build;complete;gramophone\n" );
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

/////////////////////////////////////////////////////////
// Easter Egg main quest detection.
//
// Each T6 stock map has a single notify fired on `level` when the EE main
// quest reaches its terminal state (cinematic kickoff / final showdown / etc).
// We arm a per-map watcher based on level.script and emit a one-shot
// "GSE;EE;<map>" log line. Re-emit is guarded by level.iw4m_ee_fired so even
// if the engine fires the notify twice (rare but possible) we only count once.
//
// Reference: pulled from t6-scripts-main per-map *_sq.gsc / *_achievement.gsc.
// Custom maps: no notify match → silently no-op (debug log records "no
// watcher configured" so you can spot it in server console).
/////////////////////////////////////////////////////////
WaitForEasterEggComplete()
{
    level endon( "end_game" );

    notifyName = "";
    switch ( level.script )
    {
        // zm_transit's "Tower of Babble" is BRANCHING (Maxis vs Richtofen).
        // The shared transit_sidequest_achieved notify carries no path identity
        // — both paths fire it. The C# side derives per-variant completion from
        // the terminal step flags emitted by WaitForT6EasterEggSteps below
        // (level.sq_progress["maxis"|"rich"]["FINISHED"] == 1). Don't hook the
        // shared notify here or we'd double-count + lose path identity.
        case "zm_transit":
        case "zm_highrise":
        case "zm_buried":
            // Branching map — variants drive completion via per-step terminal
            // flags emitted by WaitForT6EasterEggSteps. The shared notify
            // (transit_sidequest_achieved / highrise_sidequest_achieved /
            // buried_sidequest_achieved) carries no path identity, so don't
            // hook here.
            logprint( "[ZM-EE] Canonical notify intentionally not hooked on " + level.script + " (branching — handled by per-step watchers)\n" );
            return;
        case "zm_prison":    notifyName = "pop_goes_the_weasel_achieved"; break;
        case "zm_tomb":      notifyName = "tomb_sidequest_complete";      break;
        default:
            logprint( "[ZM-EE] No canonical EE watcher configured for map=" + level.script + "\n" );
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
// Easter Egg per-step detection (T6 — branching + multi-stage quests).
//
// Distinct from WaitForEasterEggComplete above:
//   • That hook handles single-quest maps with one terminal notify
//     (Highrise / Buried / Prison / Origins).
//   • This one handles per-step granularity for branching/multi-stage
//     quests (currently zm_transit's Tower of Babble — Maxis vs Richtofen
//     paths plus the song bears).
//
// Emits the standard pair when a step fires:
//   [ZM-EE] Step fired: <key>
//   GSE;EE;step;<key>
//
// Step keys match _PRIVATE/ZombieStatsPremium/Configuration/MapEasterEggConfig.cs.
// Unknown keys are ignored downstream — adding a watcher here without
// adding the step to the C# config is silent (warning at WRN level).
/////////////////////////////////////////////////////////
WaitForT6EasterEggSteps()
{
    level endon( "end_game" );

    switch ( level.script )
    {
        case "zm_transit":
            level thread WatchT6MeteorCounterSong( "t6_tr_bear" );
            level thread WatchT6TransitMaxisPath();
            level thread WatchT6TransitRichPath();
            break;
        case "zm_nuked":
            // Nuketown has 2 song EEs: a population-trigger one ("Won't Back
            // Down" at zombie_pop==15) and the 3-bear meteor-counter one
            // ("Samantha's Lullaby"). We track the bears only — the population
            // trigger isn't player-driven (it auto-fires by standing still).
            level thread WatchT6MeteorCounterSong( "t6_nk_bear" );
            break;
        case "zm_highrise":
            // Die Rise: song bears + branching "High Maintenance". Pre-branch
            // shared stages (atd, slb) deliberately not hooked — see config
            // doc-comment for rationale.
            level thread WatchT6MeteorCounterSong( "t6_dr_bear" );
            level thread WatchT6HighriseMaxisPath();
            level thread WatchT6HighriseRichPath();
            level thread WatchT6HighriseTerminal();
            break;
        case "zm_buried":
            // Buried: song bears + branching "Mined Games". Both paths share
            // the same stage notifies (bt/mta/gl/ftl/ll/ctw/ip/ows) — path
            // identity is determined at emit-time by inspecting the
            // sq_is_max_tower_built / sq_is_ric_tower_built flags set during
            // the bt (Build Tower) stage when the player commits to a buildable.
            level thread WatchT6MeteorCounterSong( "t6_br_bear" );
            level thread WatchT6BuriedStages();
            level thread WatchT6BuriedTerminals();
            break;
        case "zm_prison":
            // Mob of the Dead: 3 quests.
            //   • Rusty Cage (3 bottles) — meteor_counter pattern.
            //   • Where Are We Going (115 then 935 nixie) — 2 stage notifies.
            //   • Pop Goes the Weasel — 6 progress steps + canonical
            //     (pop_goes_the_weasel_achieved already in WaitForEasterEggComplete).
            level thread WatchT6CounterSong( "t6_md_bottle", ::GetMeteorCounter, 3 );
            level thread WatchT6LevelNotify( "nixie_115", "t6_md_nixie_1" );
            level thread WatchT6LevelNotify( "nixie_935", "t6_md_nixie_2" );
            level thread WatchT6PrisonPgw();
            break;
        case "zm_tomb":
            // Origins: 3 song EEs (Archangel meteorites / Shepherd of Fire
            // radios / Aether generator numbers) + 8-step "Little Lost Girl"
            // main quest. Main quest is single-path — canonical hook
            // (tomb_sidequest_complete) already in WaitForEasterEggComplete
            // above; per-step watchers add granular progress markers.
            level thread WatchT6CounterSong( "t6_or_meteor", ::GetMeteorCounter, 3 );
            level thread WatchT6CounterSong( "t6_or_radio",  ::GetRadioCounter,  3 );
            level thread WatchT6CounterSong( "t6_or_115",    ::Get115Counter,    3 );
            level thread WatchT6OriginsLittleGirlLost();
            break;
        default:
            // No per-step watcher configured for this map. Silent — the
            // canonical hook above still fires for single-quest maps.
            return;
    }

    logprint( "[ZM-EE] Per-step watchers armed for map=" + level.script + "\n" );
}

EmitEeStep( stepKey )
{
    // Idempotency: in-process dedup so a watcher that polls a flag which
    // gets reset and re-flipped doesn't double-emit. C# event processor
    // also dedups (HashSet add), so this is belt-and-braces — but cheaper
    // to suppress here than to log+drop on the C# side.
    if ( !IsDefined( level.iw4m_ee_steps_fired ) )
    {
        level.iw4m_ee_steps_fired = [];
    }
    if ( IsDefined( level.iw4m_ee_steps_fired[ stepKey ] ) )
    {
        return;
    }
    level.iw4m_ee_steps_fired[ stepKey ] = 1;

    roundStr = "?";
    if ( IsDefined( level.round_number ) ) { roundStr = "" + level.round_number; }
    logprint( "[ZM-EE] Step fired: " + stepKey + " round=" + roundStr + "\n" );
    logprint( "GSE;EE;step;" + stepKey + "\n" );
}

// Polls level.sq_progress[group][key] until it flips to 1, then emits the
// step. Won't re-arm — first transition wins (stage flags can reset to 0
// on rollback paths in stock script; we want "did the player reach this
// stage at least once" semantics).
WatchT6SqProgress( group, key, stepKey )
{
    level endon( "end_game" );

    // Guard against init order: sq_progress is built inside sidequest_init_tracker
    // which runs after a "start_zombie_round_logic" flag_wait. Poll until ready.
    while ( !IsDefined( level.sq_progress )
         || !IsDefined( level.sq_progress[ group ] )
         || !IsDefined( level.sq_progress[ group ][ key ] ) )
    {
        wait ( 1.0 );
    }

    while ( level.sq_progress[ group ][ key ] != 1 )
    {
        wait ( 0.5 );
    }

    EmitEeStep( stepKey );
}

// One-shot notify watcher — waits for a level notify and emits a step. Used
// for stock _zombiemode_sidequests stage transitions which fire predictable
// "<questId>_<stageId>_over" notifies on `level` when each stage completes.
WatchT6LevelNotify( notifyName, stepKey )
{
    level endon( "end_game" );
    level waittill( notifyName );
    EmitEeStep( stepKey );
}

WatchT6HighriseMaxisPath()
{
    level endon( "end_game" );

    // Stock: sidequest_logic_2() fires sq_2_ssp_2_over and sq_2_pts_2_over.
    // Terminal handled separately by WatchT6HighriseTerminal so we don't
    // double-emit when both paths converge on sq_tower_active.
    level thread WatchT6LevelNotify( "sq_2_ssp_2_over", "t6_dr_maxis_a" );
    level thread WatchT6LevelNotify( "sq_2_pts_2_over", "t6_dr_maxis_b" );
}

WatchT6HighriseRichPath()
{
    level endon( "end_game" );

    level thread WatchT6LevelNotify( "sq_1_ssp_1_over", "t6_dr_rich_a" );
    level thread WatchT6LevelNotify( "sq_1_pts_1_over", "t6_dr_rich_b" );
}

// Die Rise terminal: stock fires sq_tower_active when the mahjong sequence
// is solved. Path identity is inferred from sq_<ric|max>_tower_complete
// flags which were set BEFORE the mahjong phase (in sidequest_logic_<n>
// after pts stage). Either-or — one of the two flags will be set when we
// reach this point.
WatchT6HighriseTerminal()
{
    level endon( "end_game" );

    // sq_tower_active is initialized in zm_highrise_sq.gsc init (flag_init);
    // safe to flag_wait without an IsDefined guard.
    flag_wait( "sq_tower_active" );

    if ( flag( "sq_ric_tower_complete" ) )
    {
        EmitEeStep( "t6_dr_rich_complete" );
    }
    else if ( flag( "sq_max_tower_complete" ) )
    {
        EmitEeStep( "t6_dr_maxis_complete" );
    }
    else
    {
        // Defensive — sq_tower_active should never fire without one of the
        // path-claim flags set. Log so we notice if stock script changes.
        logprint( "[ZM-EE] Die Rise sq_tower_active fired but neither tower-complete flag set\n" );
    }
}

WatchT6TransitMaxisPath()
{
    level endon( "end_game" );

    level thread WatchT6SqProgress( "maxis", "A_complete",  "t6_tr_maxis_a" );
    level thread WatchT6SqProgress( "maxis", "B_complete",  "t6_tr_maxis_b" );
    level thread WatchT6SqProgress( "maxis", "C_complete",  "t6_tr_maxis_c" );
    level thread WatchT6SqProgress( "maxis", "FINISHED",    "t6_tr_maxis_complete" );
}

WatchT6TransitRichPath()
{
    level endon( "end_game" );

    level thread WatchT6SqProgress( "rich",  "A_complete",  "t6_tr_rich_a" );
    level thread WatchT6SqProgress( "rich",  "B_complete",  "t6_tr_rich_b" );
    level thread WatchT6SqProgress( "rich",  "C_complete",  "t6_tr_rich_c" );
    level thread WatchT6SqProgress( "rich",  "FINISHED",    "t6_tr_rich_complete" );
}

// Generic flag-wait watcher — analog of WatchT6LevelNotify for code paths
// driven by flag_set rather than a notify. Defends against the flag not being
// initialized yet (init order race).
WatchT6Flag( flagName, stepKey )
{
    level endon( "end_game" );

    while ( !IsDefined( level.flag ) || !IsDefined( level.flag[ flagName ] ) )
    {
        wait ( 1.0 );
    }

    flag_wait( flagName );
    EmitEeStep( stepKey );
}

// Mob of the Dead — Pop Goes the Weasel main quest. 6 progress steps in stock-
// script execution order (zm_prison_sq_final.gsc:34-36 chain prerequisites,
// then nixie codes / audio logs / plane). Canonical terminal
// (pop_goes_the_weasel_achieved) is hooked separately via WaitForEasterEggComplete.
WatchT6PrisonPgw()
{
    level endon( "end_game" );

    level thread WatchT6Flag( "quest_completed_thrice",   "t6_md_pgw_cycle" );
    level thread WatchT6Flag( "warden_blundergat_obtained","t6_md_pgw_blundergat" );
    level thread WatchT6Flag( "spoon_obtained",           "t6_md_pgw_spoon" );
    level thread WatchT6PrisonCodes();
    level thread WatchT6PrisonAudioLogs();
    level thread WatchT6Flag( "plane_boarded",            "t6_md_pgw_plane" );
}

// 4 mobster prison numbers (101, 481, 386, 872). Stock fires per-code notify
// "nixie_final_<n>" then waittill_multiple in stage_one. Order is player-
// arbitrary so we mirror the multi-wait — emit only when all 4 land.
WatchT6PrisonCodes()
{
    level endon( "end_game" );
    level waittill_multiple( "nixie_final_386", "nixie_final_481", "nixie_final_101", "nixie_final_872" );
    EmitEeStep( "t6_md_pgw_codes" );
}

// 6 audio log drops (vox_guar_tour_vo_1 through _10 grouped into 6 plays in
// stage_two). Stock spawns level.m_headphones at first drop and deletes it at
// stage_two:258 after the loop completes. Watching the IsDefined transition
// is cleaner than chaining individual sound-done notifies.
WatchT6PrisonAudioLogs()
{
    level endon( "end_game" );

    while ( !IsDefined( level.m_headphones ) )
    {
        wait ( 1.0 );
    }
    while ( IsDefined( level.m_headphones ) )
    {
        wait ( 1.0 );
    }
    EmitEeStep( "t6_md_pgw_logs" );
}

// Origins "Little Lost Girl" main quest — 8 sequential stages, each fires
// little_girl_lost_step_<N>_over notify (zm_tomb_ee_main.gsc:83-104). Single
// path — canonical (tomb_sidequest_complete) hooked separately. Step 8's _over
// fires at functionally identical time to the canonical, so no double-emit
// concern (EmitEeStep dedups by step key anyway).
WatchT6OriginsLittleGirlLost()
{
    level endon( "end_game" );

    level thread WatchT6LevelNotify( "little_girl_lost_step_1_over", "t6_or_llg_1" );
    level thread WatchT6LevelNotify( "little_girl_lost_step_2_over", "t6_or_llg_2" );
    level thread WatchT6LevelNotify( "little_girl_lost_step_3_over", "t6_or_llg_3" );
    level thread WatchT6LevelNotify( "little_girl_lost_step_4_over", "t6_or_llg_4" );
    level thread WatchT6LevelNotify( "little_girl_lost_step_5_over", "t6_or_llg_5" );
    level thread WatchT6LevelNotify( "little_girl_lost_step_6_over", "t6_or_llg_6" );
    level thread WatchT6LevelNotify( "little_girl_lost_step_7_over", "t6_or_llg_7" );
    level thread WatchT6LevelNotify( "little_girl_lost_step_8_over", "t6_or_llg_8" );
}

// Buried path-aware step emit. Both Mined Games variants share stage notifies
// in stock; the path identity comes from the per-side flag set during the bt
// (Build Tower) stage when the player commits to a buildable. Resolves the
// active variant by inspecting both flags and emits the appropriate variant's
// step key. Skips emit if neither flag is set (bt hasn't completed yet) — the
// step will fire when the relevant stage notifies.
EmitT6BuriedStep( stepSuffix )
{
    if ( IsDefined( level.flag ) && IsDefined( level.flag[ "sq_is_max_tower_built" ] ) && level.flag[ "sq_is_max_tower_built" ] )
    {
        EmitEeStep( "t6_br_maxis_" + stepSuffix );
    }
    else if ( IsDefined( level.flag ) && IsDefined( level.flag[ "sq_is_ric_tower_built" ] ) && level.flag[ "sq_is_ric_tower_built" ] )
    {
        EmitEeStep( "t6_br_rich_" + stepSuffix );
    }
    else
    {
        // bt stage hasn't set a path flag yet — drop the step. Should never
        // happen in practice (path is determined by the bt completion which
        // is itself the first hooked stage), but if stock script ever changes
        // the flag-set order, log so we notice.
        logprint( "[ZM-EE] Buried stage suffix '" + stepSuffix + "' fired but no path flag set\n" );
    }
}

// Hook each shared stage notify and route to the active variant's step key.
// Stages map to walkthrough steps (a-h). tpo (Time Bomb placement) is a
// preparation phase — not its own walkthrough step — so we hook ip (the
// switches/bells phase) instead and treat them as combined "Step 7".
WatchT6BuriedStages()
{
    level endon( "end_game" );

    level thread WatchT6BuriedSimpleStage( "sq_bt_over",  "a" );  // build tower
    level thread WatchT6BuriedSimpleStage( "sq_mta_over", "b" );  // orbs
    level thread WatchT6BuriedSimpleStage( "sq_gl_over",  "c" );  // lantern grab
    level thread WatchT6BuriedSimpleStage( "sq_ftl_over", "d" );  // power lantern
    level thread WatchT6BuriedSimpleStage( "sq_ll_over",  "e" );  // lantern placed
    level thread WatchT6BuriedWispStage();                          // f — ts/ctw loop with success guard
    level thread WatchT6BuriedSimpleStage( "sq_ip_over",  "g" );  // bells / maze switches
    level thread WatchT6BuriedSimpleStage( "sq_ows_over", "h" );  // make a wish
}

WatchT6BuriedSimpleStage( notifyName, stepSuffix )
{
    level endon( "end_game" );
    level waittill( notifyName );
    EmitT6BuriedStep( stepSuffix );
}

// The decipher/wisp stages (ts then ctw) are inside a while(!flag("sq_wisp_success"))
// retry loop in stock script — players can fail the wisp follow and have to
// re-decipher. Only emit step f on a SUCCESSFUL completion. Loops on each
// ctw_over until sq_wisp_success is set.
WatchT6BuriedWispStage()
{
    level endon( "end_game" );

    while ( true )
    {
        level waittill( "sq_ctw_over" );
        if ( IsDefined( level.flag ) && IsDefined( level.flag[ "sq_wisp_success" ] ) && level.flag[ "sq_wisp_success" ] )
        {
            EmitT6BuriedStep( "f" );
            return;
        }
        // Failed iteration — wait for next ctw_over (player retries the loop).
    }
}

// Per-side terminals — fired explicitly by stock after the path-determination
// flag check at zm_buried_sq.gsc:380-393. Cleaner than inferring from the
// shared buried_sidequest_achieved notify because path identity is unambiguous.
WatchT6BuriedTerminals()
{
    level endon( "end_game" );

    level thread WatchT6LevelNotify( "sq_maxis_complete",     "t6_br_maxis_complete" );
    level thread WatchT6LevelNotify( "sq_richtofen_complete", "t6_br_rich_complete" );
}

// Generic counter-based song watcher — used by every T6 map whose song EE
// follows the "N hardcoded origins, counter increments per hit, song fires at
// target" pattern (most T6 song EEs). Polls the value returned by the getter
// function pointer and emits stepKeyPrefix + "_1" / "_2" / "_n" on each
// transition. First-transition-wins per step (EmitEeStep dedups). Returns
// once counter hits target.
//
// Function-pointer indirection because GSC can't dynamically read level[<str>]
// — each counter has its own dedicated reader (GetMeteorCounter, GetRadioCounter,
// Get115Counter) below. Adding a new counter = add a new getter + pass it here.
WatchT6CounterSong( stepKeyPrefix, counterGetter, target )
{
    level endon( "end_game" );

    // Stock map-init runs the counter setup before WaitForT6EasterEggSteps
    // is armed — but the counter var may not be set yet on race. Defend.
    while ( !IsDefined( [[ counterGetter ]]() ) )
    {
        wait ( 1.0 );
    }

    lastSeen = 0;
    while ( true )
    {
        cur = [[ counterGetter ]]();
        if ( cur > lastSeen )
        {
            // Walk every value we crossed (in case multiple ticks happen
            // between polls), capped at target.
            for ( i = lastSeen + 1; i <= cur && i <= target; i++ )
            {
                EmitEeStep( stepKeyPrefix + "_" + i );
            }
            lastSeen = cur;
            if ( cur >= target )
            {
                return;
            }
        }
        wait ( 0.5 );
    }
}

GetMeteorCounter()      { if ( !IsDefined( level.meteor_counter ) )         return undefined; return level.meteor_counter; }
GetRadioCounter()       { if ( !IsDefined( level.found_ee_radio_count ) )   return undefined; return level.found_ee_radio_count; }
Get115Counter()         { if ( !IsDefined( level.snd115count ) )            return undefined; return level.snd115count; }

// Backwards-compatible wrapper for the legacy meteor-counter callers
// (TranZit / Nuketown / Die Rise / Buried / Mob Rusty). Routes to the
// generic watcher with the meteor_counter getter and target=3 (every map
// that uses meteor_counter has a 3-bear EE).
WatchT6MeteorCounterSong( stepKeyPrefix )
{
    level thread WatchT6CounterSong( stepKeyPrefix, ::GetMeteorCounter, 3 );
}

