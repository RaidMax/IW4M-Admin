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
