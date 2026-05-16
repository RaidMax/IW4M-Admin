#using scripts\codescripts\struct;
#using scripts\shared\array_shared;
#using scripts\shared\callbacks_shared;
#using scripts\shared\flag_shared;
#using scripts\shared\system_shared;
#using scripts\shared\util_shared;
#using scripts\shared\ai\zombie_utility;
#using scripts\zm\_zm_utility;
#using scripts\zm\_zm_weapons;

#insert scripts\shared\shared.gsh;

// ─────────────────────────────────────────────────────────────────
// T7 Zombie Stats — Game Log Event Emitter
// Port of _zm_stats_t6.gsc to T7 (Black Ops 3 / T7x AlterWare).
//
// Key T7 differences vs T6:
//   1. ZM bypasses the T7-native callback::callback() dispatcher
//      (_zm.gsc:1504-1511 sets level.callback* to ZM-specific fns
//      which never chain into the dispatcher). So combat hooks use
//      the legacy level.callback* override + chain-to-Original
//      pattern, same as T6. Watchdog re-installs over map overrides
//      (zm_tomb.gsc:244 sets &tomb_actor_damage_override_wrapper).
//   2. Engine signatures differ from T6: actordamage 15 args (vs 11),
//      playerdamage 13 args (vs 11). Killed/laststand unchanged.
//   3. _zm_buildables system replaced — buildables coverage dropped
//      for v1. Craftables (zm_craftables.gsc) still present and
//      hooked.
//   4. Easter Egg coverage is placeholder-only for v1 — per-map
//      structure preserved with empty bodies so the wiring is
//      visible in code.
//
// Event log format (matches T6 — IW4MAdmin parser is shared):
//   GSE;AD;<victim>;<attacker>;<weapon>;<dmg>;<mod>;<hitloc>   = actor damage
//   GSE;AK;<victim>;<attacker>;<weapon>;<dmg>;<mod>;<hitloc>   = actor kill
//   GSE;D;<victim>;<attacker>;<weapon>;<dmg>;<mod>;<hitloc>    = player damage
//   GSE;K;<victim>;<attacker>;...                              = player kill / zombified
//   GSE;RD;<player>;<totalScore>;<curScore>;<round>;<gameOver> = round data per player
//   GSE;RC;<round>                                             = round complete
//   GSE;ZP;<player>;<action>;...                               = player action (perk/box/etc)
//   GSE;ZW;...                                                 = world-scope event
// ─────────────────────────────────────────────────────────────────

#namespace zombie_stats;

REGISTER_SYSTEM( "zombie_stats", &__init__, undefined )

function __init__()
{
    callback::on_start_gametype( &init );
}

function init()
{
    // Bootstrap dvars for IW4MAdmin recovery on RCon reconnect mid-match.
    setdvar( "sv_iw4m_zm_round", 1 );
    setdvar( "sv_iw4m_zm_matchid", "" + randomint( 1000000 ) + "_" + randomint( 1000000 ) );

    // T7x engine emits "InitGame: map zm_zod; gametype zclassic;" — the
    // semicolons short-circuit BaseEventParser.GetEventTypeFromLine through
    // the `;`-split path before the .*InitGame.* regex can match, so MapChange
    // never fires and MatchStartEvent is never dispatched. Plutonium/CoD-stock
    // format uses `\key\val\key\val` (no `;`) which falls through to the regex.
    // Emit a synthetic CoD-stock-shaped InitGame so the parser fires MapChange.
    // The engine line still lands in the log as Unknown noise but no longer
    // gates match start.
    logprint( "InitGame: \\mapname\\" + getdvarstring( "mapname" ) + "\\g_gametype\\" + getdvarstring( "g_gametype" ) + "\n" );

    // Engine-event callbacks (T7-native append-mode).
    SetupCallbacks();

    // Periodic / level-scoped watchers.
    thread WaitForRoundChange();
    thread WaitForPlayerConnect();
    thread WaitForPowerupSpawned();
    thread WaitForWeaponPurchases();
    thread WaitForPackAPunch();
    thread WaitForDoorPurchases();
    thread WaitForMysteryBox();
    thread WaitForBoxTeddySuppression();
    thread WaitForTrapActivations();
    thread WaitForCraftables();
    thread WaitForEasterEggComplete();    // stub for v1 — wiring only
    thread WaitForT7EasterEggSteps();     // stub for v1 — wiring only
    thread WatchPowerSwitches();
    thread WatchPowerStateChanges();
    thread WatchZombiesRemaining();
    thread WaitForGobbleGumMachines();
}

function SetupCallbacks()
{
    // ZM bypasses callback::callback() dispatcher (set in _zm.gsc:1504-1511).
    // Must override level.callback* legacy hooks and chain to original.
    // Engine signatures (cp/_globallogic_actor.gsc: callback_X dispatch):
    //   playerdamage    = 13 args
    //   actordamage     = 15 args (T7 adds vdamageorigin, modelindex, surfacetype, vsurfacenormal vs T6's 11)
    //   actorkilled     =  8 args (same as T6)
    //   playerlaststand =  9 args (same as T6)
    waittillframeend;

    level.callbackActorDamageOriginal = level.callbackactordamage;
    level.callbackActorKilledOriginal = level.callbackactorkilled;
    level.callbackactordamage = &OnActorDamage;
    level.callbackactorkilled = &OnActorKilled;

    level.callbackPlayerDamageOriginal = level.callbackplayerdamage;
    level.callbackplayerdamage = &OnPlayerDamaged;

    level.callbackPlayerLastStandOriginal = level.callbackplayerlaststand;
    level.callbackplayerlaststand = &OnPlayerDowned;

    // Maps that overwrite level.callback* post-init (e.g. zm_tomb.gsc:244
    // sets &tomb_actor_damage_override_wrapper). Periodic re-install captures
    // map fn as new Original to chain through.
    thread WatchdogCallbacks();
}

function WatchdogCallbacks()
{
    // 1s grace for map setup to finish.
    wait ( 1 );

    for ( ;; )
    {
        if ( level.callbackactordamage != &OnActorDamage )
        {
            level.callbackActorDamageOriginal = level.callbackactordamage;
            level.callbackactordamage = &OnActorDamage;
        }

        if ( level.callbackactorkilled != &OnActorKilled )
        {
            level.callbackActorKilledOriginal = level.callbackactorkilled;
            level.callbackactorkilled = &OnActorKilled;
        }

        if ( level.callbackplayerdamage != &OnPlayerDamaged )
        {
            level.callbackPlayerDamageOriginal = level.callbackplayerdamage;
            level.callbackplayerdamage = &OnPlayerDamaged;
        }

        if ( level.callbackplayerlaststand != &OnPlayerDowned )
        {
            level.callbackPlayerLastStandOriginal = level.callbackplayerlaststand;
            level.callbackplayerlaststand = &OnPlayerDowned;
        }

        wait ( 5 );
    }
}

//-----------------//
//---- Waiters ----//
//-----------------//

function WaitForPlayerConnect()
{
    for ( ;; )
    {
        level waittill( "connecting", player );

        player thread WaitForPlayerRevive();
        player thread WaitForPlayerZombified();
        player thread WaitForPerkBought();
        player thread WaitForGobbleGumActivate();
    }
}

function WaitForGobbleGumActivate()
{
    self endon( "disconnect" );

    for ( ;; )
    {
        // _zm_bgb.gsc:905 fires `self notify(#"bgb_activation", self.bgb)` when
        // a player consumes an "activated" limit_type Gobble Gum (Perkaholic,
        // Anywhere But Here, etc.). Auto-trigger types (time/rounds/event) start
        // silently — not covered here.
        self waittill( "bgb_activation", bgbName );
        if ( !IsDefined( bgbName ) ) { bgbName = "undef"; }
        logprint( "GSE;ZP;" + BuildPlayerInfoString( self ) + ";gum;activate;" + bgbName + "\n" );
    }
}

function WaitForGobbleGumMachines()
{
    // Settle window — _zm_bgb_machine.gsc:281 sets up the array via getentarray
    // during init. 3s mirrors WaitForCraftables and is safe across maps.
    wait ( 3 );

    if ( !IsDefined( level.bgb_machines ) ) { return; }

    for ( i = 0; i < level.bgb_machines.size; i++ )
    {
        level.bgb_machines[i] thread WatchGobbleGumTake();
    }
}

function WatchGobbleGumTake()
{
    for ( ;; )
    {
        // _zm_bgb_machine.gsc:736 — gumball_available fires once selected_bgb +
        // current_cost are locked AND cost was deducted at line 699. Snapshot
        // here so we still have the data if the machine resets state quickly.
        self waittill( "gumball_available" );

        offeredUser = self.bgb_machine_user;
        offeredGum = "undef";
        if ( IsDefined( self.selected_bgb ) ) { offeredGum = self.selected_bgb; }
        offeredCost = 0;
        if ( IsDefined( self.current_cost ) ) { offeredCost = self.current_cost; }
        ghostBall = IsDefined( self.b_bgb_is_available ) && !self.b_bgb_is_available;

        // Race the two terminal notifies of the machine cycle.
        //   user_grabbed_bgb (line 771)  → take path; bgb_machine_accessed will
        //                                  trail moments later — drain to keep
        //                                  next iteration aligned.
        //   bgb_machine_accessed (line 831) → end-of-cycle without a take. If
        //                                     b_bgb_is_available was false this
        //                                     is a refunded ghost-ball, NOT a
        //                                     player-driven leave.
        outcome = self util::waittill_any_return( "user_grabbed_bgb", "bgb_machine_accessed" );

        if ( !IsDefined( offeredUser ) || !IsPlayer( offeredUser ) ) { continue; }

        if ( outcome == "user_grabbed_bgb" )
        {
            logprint( "GSE;ZP;" + BuildPlayerInfoString( offeredUser ) + ";gum;take;" + offeredGum + ";" + offeredCost + "\n" );
            self waittill( "bgb_machine_accessed" );
        }
        else if ( !ghostBall )
        {
            // Cost was deducted upfront and not refunded — player paid for nothing.
            logprint( "GSE;ZP;" + BuildPlayerInfoString( offeredUser ) + ";gum;leave;" + offeredGum + ";" + offeredCost + "\n" );
        }
    }
}

function WaitForPlayerZombified()
{
    self endon( "disconnect" );

    for ( ;; )
    {
        // T7: _zm.gsc fires self notify(#"zombified") when downed player
        // is moved to spectator after revive timer expires.
        self waittill( "zombified" );
        playerInfo = BuildPlayerInfoString( self );
        logprint( "GSE;K;" + playerInfo + ";ffffffff;-1;axis;Zombie;default_weapon;0;MOD_MELEE;none\n" );
    }
}

function WaitForPlayerRevive()
{
    self endon( "disconnect" );

    for ( ;; )
    {
        // Same notify as T6. T7 _zm_laststand fires this on the revivee
        // with the reviver as the second arg.
        self waittill( "player_revived", reviver );

        // Self-revive (solo Quick Revive auto / Self Revive gobblegum):
        // reviver==self. Emit distinct subtype so downstream classification
        // doesn't need guid compare.
        if ( IsDefined( reviver ) && IsPlayer( reviver ) && reviver == self )
        {
            logprint( "GSE;ZP;" + BuildPlayerInfoString( self ) + ";revive;self\n" );
        }
        else
        {
            logprint( "GSE;ZP;" + BuildPlayerInfoString( self ) + ";revive;" + BuildPlayerInfoString( reviver ) + "\n" );
        }
    }
}

function WaitForPerkBought()
{
    self endon( "disconnect" );

    for ( ;; )
    {
        // T7 _zm_perks fires self notify(#"perk_bought", perk) — confirmed
        // by zm_moon_achievement.gsc waiting on it.
        self waittill( "perk_bought", perk );
        // Cost not exposed by the notify (engine hardcodes per-perk).
        logprint( "GSE;ZP;" + BuildPlayerInfoString( self ) + ";perk;buy;" + perk + ";0\n" );
    }
}

function WaitForRoundChange()
{
    for ( ;; )
    {
        result = level util::waittill_any_return( "intermission", "between_round_over" );

        players = getplayers();

        for ( i = 0; i < players.size; i++ )
        {
            // Already-zombified players are emitted by WaitForPlayerZombified.
            if ( IsDefined( players[i].is_zombie ) && players[i].is_zombie )
            {
                continue;
            }
            if ( zombie_utility::get_current_zombie_count() == 0 )
            {
                continue;
            }
            playerInfo = BuildPlayerInfoString( players[i] );
            logprint( "GSE;K;" + playerInfo + ";ffffffff;-1;axis;Zombie;default_weapon;0;MOD_MELEE;none\n" );
        }

        // Ensure K events drain before RD/RC. IW4MAdmin event processing
        // is concurrent; same-frame K+RD can race and drop Deaths stat.
        wait ( 0.1 );

        isGameOver = IsDefined( result ) && result == "intermission";
        PrintPlayerRoundData( isGameOver );

        if ( isGameOver )
        {
            // Synthetic ExitLevel — T7x doesn't write the native engine line
            // on game-over, so we fabricate one that matches IW4MAdmin's
            // MapEnd regex (BaseEventParser.cs:104 — .*(?:ExitLevel|ShutdownGame).*).
            // Without this, MatchEndEvent never fires on T7, OnMatchEnded
            // never runs, premium cleanup never happens, ZombieRoundNumber
            // stays stale, and EFZombieMatch.Completed never sets. Must NOT
            // have a "GSE;" prefix — that branch routes to GameScriptEvent
            // BEFORE the MapEnd regex would match.
            logprint( "ExitLevel: zombie match ended\n" );
            break;
        }

        // Detect special-round type for the round about to begin so IW4MAdmin can
        // flag it in the breakdown UI and skip SPH (special spawn budgets don't
        // match the regular zombie formula). Single-fire per round transition.
        EmitSpecialRoundIfAny();
    }
}

// T7 special-round flags. Each map-set's _zm_ai_<type>.gsc sets its flag when
// the round starts (and "special_round" alongside as a generic gate). Direct
// level.flag[name] dict access is portable across maps — flag::get would
// assert on un-initialised flags on maps that don't load that AI script.
function EmitSpecialRoundIfAny()
{
    if ( !IsDefined( level.flag ) ) { return; }

    specialType = "";
    if      ( IsDefined( level.flag[ "dog_round" ] )               && level.flag[ "dog_round" ] )               { specialType = "dog"; }
    else if ( IsDefined( level.flag[ "monkey_round" ] )            && level.flag[ "monkey_round" ] )            { specialType = "monkey"; }
    else if ( IsDefined( level.flag[ "wasp_round" ] )              && level.flag[ "wasp_round" ] )              { specialType = "wasp"; }
    else if ( IsDefined( level.flag[ "spiders_from_mars_round" ] ) && level.flag[ "spiders_from_mars_round" ] ) { specialType = "spider"; }
    else if ( IsDefined( level.flag[ "three_robot_round" ] )       && level.flag[ "three_robot_round" ] )       { specialType = "robot"; }
    else if ( IsDefined( level.flag[ "special_quad_round" ] )      && level.flag[ "special_quad_round" ] )      { specialType = "quad"; }
    else if ( IsDefined( level.flag[ "boss_round" ] )              && level.flag[ "boss_round" ] )              { specialType = "boss"; }
    else if ( IsDefined( level.flag[ "ee_round" ] )                && level.flag[ "ee_round" ] )                { specialType = "ee"; }

    if ( specialType != "" )
    {
        logprint( "GSE;ZW;round_special;" + level.round_number + ";" + specialType + "\n" );
    }
}

//-------------------//
//---- Callbacks ----//
//-------------------//

// T7 ZM actor-damage signature (15 args — _zm.gsc:6083 actor_damage_override_wrapper).
// Adds vDamageOrigin, modelIndex, surfaceType, vSurfaceNormal vs T6's 11.
// T7 weapon param is a struct (WeaponObject) — use .name for string. Engine's own
// logprint at _globallogic_actor.gsc:212 does the same.
function OnActorDamage( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, vDamageOrigin, psOffsetTime, boneIndex, modelIndex, surfaceType, vSurfaceNormal )
{
    if ( IsPlayer( eInflictor ) || IsPlayer( eAttacker ) || IsPlayer( self ) )
    {
        victimInfo = BuildPlayerInfoString( self );
        attackerInfo = BuildPlayerInfoString( eAttacker );
        if ( IsPlayer( eInflictor ) ) { attackerInfo = BuildPlayerInfoString( eInflictor ); }
        weaponName = WeaponName( sWeapon );

        // Skip on lethal hit — kill callback covers it.
        if ( IsDefined( self.health ) && self.health > 0 )
        {
            reportedDamage = iDamage;
            if ( IsDefined( self.maxhealth ) && self.maxhealth > 0 && reportedDamage > self.maxhealth )
            {
                reportedDamage = self.maxhealth;
            }
            logprint( "GSE;AD;" + victimInfo + ";" + attackerInfo + ";" + weaponName + ";" + reportedDamage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
        }
    }

    self [[ level.callbackActorDamageOriginal ]]( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, vDamageOrigin, psOffsetTime, boneIndex, modelIndex, surfaceType, vSurfaceNormal );
}

// T7 ZM actor-killed signature (8 args — _zm.gsc:6136 actor_killed_override). Same as T6.
function OnActorKilled( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime )
{
    if ( IsPlayer( eInflictor ) || IsPlayer( eAttacker ) || IsPlayer( self ) )
    {
        victimInfo = BuildPlayerInfoString( self );
        attackerInfo = BuildPlayerInfoString( eAttacker );
        if ( IsPlayer( eInflictor ) ) { attackerInfo = BuildPlayerInfoString( eInflictor ); }
        weaponName = WeaponName( sWeapon );

        damage = iDamage;
        if ( IsDefined( self.maxhealth ) && self.maxhealth > 0 && damage > self.maxhealth )
        {
            damage = self.maxhealth;
        }
        logprint( "GSE;AK;" + victimInfo + ";" + attackerInfo + ";" + weaponName + ";" + damage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
    }

    self [[ level.callbackActorKilledOriginal ]]( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime );
}

// T7 ZM player-damage signature (13 args — _zm.gsc:1599 callback_playerdamage).
// Adds vDamageOrigin, vSurfaceNormal vs T6's 11.
function OnPlayerDamaged( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, vDamageOrigin, psOffsetTime, boneIndex, vSurfaceNormal )
{
    if ( IsPlayer( eInflictor ) || IsPlayer( eAttacker ) || IsPlayer( self ) )
    {
        victimInfo = BuildPlayerInfoString( self );
        attackerInfo = BuildPlayerInfoString( eAttacker );
        if ( IsPlayer( eInflictor ) ) { attackerInfo = BuildPlayerInfoString( eInflictor ); }
        weaponName = WeaponName( sWeapon );

        logprint( "GSE;D;" + victimInfo + ";" + attackerInfo + ";" + weaponName + ";" + iDamage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
    }

    self [[ level.callbackPlayerDamageOriginal ]]( eInflictor, eAttacker, iDamage, iDFlags, sMeansOfDeath, sWeapon, vPoint, vDir, sHitLoc, vDamageOrigin, psOffsetTime, boneIndex, vSurfaceNormal );
}

// T7 ZM laststand signature (9 args — _zm.gsc:1536 callback_playerlaststand). Same as T6.
function OnPlayerDowned( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration )
{
    // De-dupe re-fires while player is still downed.
    if ( !IsDefined( self.revivetrigger ) )
    {
        logprint( "GSE;ZP;" + BuildPlayerInfoString( self ) + ";down\n" );
    }

    self [[ level.callbackPlayerLastStandOriginal ]]( eInflictor, eAttacker, iDamage, sMeansOfDeath, sWeapon, vDir, sHitLoc, psOffsetTime, deathAnimDuration );
}

//-----------------//
//---- Helpers ----//
//-----------------//

function PrintPlayerRoundData( isGameOver )
{
    if ( !IsDefined( level.round_number ) )
    {
        return;
    }

    players = getplayers();
    currentRound = level.round_number;

    for ( i = 0; i < players.size; i++ )
    {
        if ( IsDefined( players[i].sessionstate ) && players[i].sessionstate == "spectator" )
        {
            continue;
        }

        totalScore = 0;
        currentScore = 0;
        if ( IsDefined( players[i].score_total ) ) { totalScore = players[i].score_total; }
        if ( IsDefined( players[i].score ) ) { currentScore = players[i].score; }

        logprint( "GSE;RD;" + BuildPlayerInfoString( players[i] ) + ";" + totalScore + ";" + currentScore + ";" + currentRound + ";" + isGameOver + "\n" );
    }

    wait ( 0.1 );
    setdvar( "sv_iw4m_zm_round", currentRound );
    logprint( "GSE;RC;" + currentRound + "\n" );
}

function BuildPlayerInfoString( entity )
{
    if ( IsPlayer( entity ) )
    {
        // T7x getGuid() returns 0 in offline-style sessions; getxuid() returns
        // the Steam XUID hex string that matches RCon status + EFClients.NetworkId.
        // Parser default GuidNumberStyle=HexNumber throws FormatException on "0".
        guid = entity getxuid();
        clientNumber = entity getEntityNumber();
        team = entity.team;
        name = entity.name;
        if ( !IsDefined( name ) ) { name = "null"; }
        return guid + ";" + clientNumber + ";" + team + ";" + name;
    }
    // "ffffffff" not "-1" — HexNumber parser throws on signed "-1".
    return "ffffffff;-1;axis;Zombie";
}

// T7 weapons are structs; string-coerce safely. Accepts a WeaponObject, a string,
// or undefined. Returns the engine name ("ar_modern_zm" etc.) or "none"/"undef"
// fallbacks so log lines never include an entity pointer.
function WeaponName( w )
{
    if ( !IsDefined( w ) ) { return "undef"; }
    if ( IsString( w ) )   { return w; }
    if ( IsDefined( w.name ) ) { return w.name; }
    return "undef";
}

//----------------//
//---- Powerups ----//
//----------------//

// Event-driven powerup detection. Engine fires:
//   level notify("powerup_dropped", powerup)  — on every drop (zombie kill, gum, scripted)
//   self  notify("powerup_grabbed")           — on the entity at engine-grab time
//
// **STRING-FORM notify ONLY**. Hashed `#"powerup_dropped"` / `#"powerup_grabbed"` do NOT
// fire in T7x's runtime — verified empirically 2026-05-14 via three-way detection race.
// Hash mismatch between our linker and the engine; shiversoftdev decompile shows
// these notifies as `#"..."` but the names are reverse-lookup guesses and the actual
// engine hashes differ. Use STRING form for all engine-emitted notifies on T7x.
//
// Polling at script-init catches one-shot pre-existing entities (powerups spawned by
// init scripts before our notify listeners attached). After init, the notify path
// handles everything. iw4m_pwr_seen sentinel deduplicates poll vs notify pickups.
function WaitForPowerupSpawned()
{
    level thread WatchPowerupSpawnNotify();
    level thread WatchPowerupSpawnInitScan();
}

function WatchPowerupSpawnNotify()
{
    for ( ;; )
    {
        level waittill( "powerup_dropped", powerup );
        if ( !IsDefined( powerup ) ) { continue; }
        if ( IsDefined( powerup.iw4m_pwr_seen ) ) { continue; }
        powerup.iw4m_pwr_seen = true;
        powerup thread WaitForPowerupGrab();
    }
}

// One-shot scan to catch powerups already in-world when our threads started
// (e.g. spawned during map init before our notify listener attached).
function WatchPowerupSpawnInitScan()
{
    wait ( 0.05 );
    models = getentarray( "script_model", "classname" );
    for ( i = 0; i < models.size; i++ )
    {
        if ( !IsDefined( models[i].powerup_name ) ) { continue; }
        if ( IsDefined( models[i].iw4m_pwr_seen ) ) { continue; }
        models[i].iw4m_pwr_seen = true;
        models[i] thread WaitForPowerupGrab();
    }
}

function WaitForPowerupGrab()
{
    self endon( "powerup_timedout" );

    self waittill( "powerup_grabbed" );

    if ( !IsDefined( self ) ) { return; }

    powerup = "unknown";
    if ( IsDefined( self.powerup_name ) ) { powerup = self.powerup_name; }

    // Engine's grab notify carries no player arg — resolve grabbing player by
    // nearest-distance snapshot. The engine's own grab logic uses ~64-unit
    // proximity, so nearest-player at notify-fire time matches the engine's
    // pick with very high accuracy (solo: 100%; co-op: ~99%).
    grabber = undefined;
    bestDist = 999999;
    players = getplayers();
    for ( i = 0; i < players.size; i++ )
    {
        if ( !IsDefined( players[i] ) || !IsPlayer( players[i] ) ) { continue; }
        if ( !IsDefined( players[i].origin ) ) { continue; }
        d = distance( players[i].origin, self.origin );
        if ( d < bestDist )
        {
            bestDist = d;
            grabber = players[i];
        }
    }

    if ( !IsDefined( grabber ) ) { return; }
    logprint( "GSE;ZP;" + BuildPlayerInfoString( grabber ) + ";powerup;grab;" + powerup + "\n" );
}

//----------------//
//---- Economy ----//
//----------------//

function WaitForWeaponPurchases()
{
    for ( ;; )
    {
        // T7 _zm_weapons.gsc:2473 fires `level notify(#"weapon_bought", player, self.weapon)`.
        // Third arg is a WeaponObject struct, NOT a string. Use zm_weapons::get_weapon_cost
        // which the engine itself uses (line 2452) — does the level.zombie_weapons[struct].cost
        // lookup safely for us.
        level waittill( "weapon_bought", player, weaponObj );
        if ( !IsDefined( player ) || !IsPlayer( player ) ) { continue; }

        cost = 0;
        if ( IsDefined( weaponObj ) )
        {
            cost = zm_weapons::get_weapon_cost( weaponObj );
            if ( !IsDefined( cost ) ) { cost = 0; }
        }
        logprint( "GSE;ZP;" + BuildPlayerInfoString( player ) + ";weapon;buy;" + WeaponName( weaponObj ) + ";" + cost + "\n" );
    }
}

function WaitForDoorPurchases()
{
    wait ( 2 );

    doors = getentarray( "zombie_door", "targetname" );
    debris = getentarray( "zombie_debris", "targetname" );

    for ( i = 0; i < doors.size; i++ )  { doors[i]  thread WatchDoorPurchase(); }
    for ( i = 0; i < debris.size; i++ ) { debris[i] thread WatchDoorPurchase(); }
}

function WatchDoorPurchase()
{
    cost = 1000;
    if ( IsDefined( self.zombie_cost ) ) { cost = self.zombie_cost; }

    self waittill( "trigger", player );

    if ( !IsDefined( player ) || !IsPlayer( player ) ) { return; }
    if ( !IsDefined( player.score ) || player.score < cost ) { return; }

    logprint( "GSE;ZP;" + BuildPlayerInfoString( player ) + ";door;buy;" + cost + "\n" );
}

//----------------//
//---- Pack-a-Punch ----//
//----------------//

function WaitForPackAPunch()
{
    wait ( 2 );

    // T7: PaP triggers are published in level.pack_a_punch.triggers by
    // _zm_pack_a_punch.gsc init.
    if ( !IsDefined( level.pack_a_punch ) || !IsDefined( level.pack_a_punch.triggers ) )
    {
        return;
    }

    papTriggers = level.pack_a_punch.triggers;
    if ( papTriggers.size == 0 ) { return; }

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
// AFTER another notify resolved the iter. Ignoring later notifies prevents
// misclassification (abandon emitted as upgrade when stale take notify fires
// after timeout cleared the iter).
function WatchPapTakenFlag()
{
    for ( ;; )
    {
        self waittill( "pap_taken" );
        if ( self.iw4m_pap_taken_flag || self.iw4m_pap_timeout_flag || self.iw4m_pap_disconnect_flag ) { continue; }
        self.iw4m_pap_taken_flag = true;
    }
}

function WatchPapTimeoutFlag()
{
    for ( ;; )
    {
        self waittill( "pap_timeout" );
        if ( self.iw4m_pap_taken_flag || self.iw4m_pap_timeout_flag || self.iw4m_pap_disconnect_flag ) { continue; }
        self.iw4m_pap_timeout_flag = true;
    }
}

// T7 _zm_pack_a_punch.gsc:772 fires pap_player_disconnected when buyer leaves
// mid-PaP. Short-circuits emission — no player to credit.
function WatchPapDisconnectFlag()
{
    for ( ;; )
    {
        self waittill( "pap_player_disconnected" );
        if ( self.iw4m_pap_taken_flag || self.iw4m_pap_timeout_flag || self.iw4m_pap_disconnect_flag ) { continue; }
        self.iw4m_pap_disconnect_flag = true;
    }
}

function WatchPapTriggerForBuyer()
{
    for ( ;; )
    {
        self waittill( "trigger", who );

        if ( IsDefined( self.iw4m_pap_buyer ) ) { continue; }
        if ( !IsDefined( who ) || !IsPlayer( who ) ) { continue; }

        // Phase 2: engine accepted (self.current_weapon set to a real weapon
        // struct, not level.weaponnone). Buyer is the player whose weapon engine
        // just took — their getcurrentweapon() now returns level.weaponnone.
        // Late F-pressers in phase2 still hold their own weapon → discriminates.
        if ( IsDefined( self.current_weapon ) && self.current_weapon !== level.weaponnone )
        {
            buyerWeapon = who getcurrentweapon();
            if ( IsDefined( buyerWeapon ) && buyerWeapon !== level.weaponnone )
            {
                continue;
            }
            self.iw4m_pap_buyer = who;
            self.iw4m_pap_buyer_weapon = self.current_weapon;
            continue;
        }

        // Phase 1: pre-engine-accept. Replicate engine score + upgradeable gates
        // so we don't lock on rejected presses.
        if ( !IsDefined( who.score ) || who.score < 5000 ) { continue; }

        weapon = who getcurrentweapon();
        if ( !IsDefined( weapon ) || weapon === level.weaponnone ) { continue; }
        if ( !zm_weapons::can_upgrade_weapon( weapon ) ) { continue; }

        self.iw4m_pap_buyer = who;
        self.iw4m_pap_buyer_weapon = weapon;

        // Verify engine actually accepted; unlock otherwise. Handles engine-side
        // gates we don't replicate (laststand, throwing grenade, weapon switch).
        self thread VerifyPapBuyerLock();
    }
}

function VerifyPapBuyerLock()
{
    // Poll for engine acceptance up to 2s. T6 lesson: 0.25s wasn't long enough;
    // engine sometimes delays setting current_weapon past that → unlocks legit
    // buyer → later stale F-press re-locks wrong weapon → false mismatch →
    // skipped emit. Defensive port even though only T6 was observed failing.
    timeoutMs = 2000;
    pollMs = 50;
    elapsedMs = 0;
    while ( elapsedMs < timeoutMs )
    {
        if ( IsDefined( self.current_weapon ) && self.current_weapon !== level.weaponnone )
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

function WatchPapOutcome()
{
    for ( ;; )
    {
        self.iw4m_pap_taken_flag = false;
        self.iw4m_pap_timeout_flag = false;
        self.iw4m_pap_disconnect_flag = false;
        self.iw4m_pap_buyer = undefined;
        self.iw4m_pap_buyer_weapon = undefined;

        // Phase 1: wait for engine to accept a buy. T7 _zm_pack_a_punch.gsc:507
        // sets self.current_weapon = current_weapon (struct); cleared back to
        // level.weaponnone at line 527.
        while ( !IsDefined( self.current_weapon ) || self.current_weapon === level.weaponnone )
        {
            wait ( 0.05 );
        }

        oldWeapon = self.current_weapon;

        // Phase 2: wait for iter boundary (current_weapon clears OR engine
        // immediately starts new iter with different weapon).
        while ( IsDefined( self.current_weapon ) && self.current_weapon === oldWeapon )
        {
            wait ( 0.05 );
        }

        // Disconnect short-circuits emission (no player to credit).
        if ( self.iw4m_pap_disconnect_flag )
        {
            continue;
        }

        if ( !IsDefined( self.iw4m_pap_buyer ) || !IsPlayer( self.iw4m_pap_buyer ) )
        {
            continue;
        }

        // T7 PaP cost: self.cost = 5000 base, 1000 during sale. T7 replaces T6's
        // attachment-only re-PaP (self.attachment_cost) with the AAT system —
        // separate self.aat_cost, separate flow. Skip AAT for now; just emit
        // base/upgrade cost.
        cost = 5000;
        if ( IsDefined( self.cost ) ) { cost = self.cost; }

        if ( self.iw4m_pap_taken_flag )
        {
            // T7-native upgrade lookup via zm_weapons helper. Fallback to "<old>_upgraded"
            // string if helper returns undef on some edge case.
            upgradedWeapon = undefined;
            if ( IsDefined( oldWeapon ) && oldWeapon !== level.weaponnone )
            {
                upgradedWeapon = zm_weapons::get_upgrade_weapon( oldWeapon );
            }
            oldName = WeaponName( oldWeapon );
            newName = WeaponName( upgradedWeapon );
            if ( newName == "undef" ) { newName = oldName + "_upgraded"; }
            logprint( "GSE;ZP;" + BuildPlayerInfoString( self.iw4m_pap_buyer ) + ";weapon;upgrade;" + oldName + ";" + newName + ";" + cost + "\n" );
        }
        else if ( self.iw4m_pap_timeout_flag )
        {
            logprint( "GSE;ZP;" + BuildPlayerInfoString( self.iw4m_pap_buyer ) + ";weapon;abandon;" + WeaponName( oldWeapon ) + ";" + cost + "\n" );
        }
    }
}

//----------------//
//---- Mystery Box ----//
//----------------//

function WaitForMysteryBox()
{
    wait ( 5 );

    if ( !IsDefined( level.chests ) ) { return; }

    for ( i = 0; i < level.chests.size; i++ )
    {
        level.chests[i].iw4m_box_teddy_marker = false;
        level.chests[i].iw4m_box_in_late_phase = false;

        level.chests[i] thread WatchBoxOutcome();
        level.chests[i] thread WatchBoxTriggerForBuyer();
    }
}

function WaitForBoxTeddySuppression()
{
    wait ( 5 );
    for ( ;; )
    {
        level waittill( "weapon_fly_away_start" );
        if ( !IsDefined( level.chests ) ) { continue; }

        for ( k = 0; k < level.chests.size; k++ )
        {
            if ( IsDefined( level.chests[k].iw4m_box_in_late_phase ) && level.chests[k].iw4m_box_in_late_phase )
            {
                level.chests[k].iw4m_box_teddy_marker = true;
            }
        }
    }
}

function WatchBoxTriggerForBuyer()
{
    for ( ;; )
    {
        self waittill( "trigger", who );

        if ( IsDefined( self.iw4m_box_last_trigger ) ) { continue; }
        if ( !IsDefined( who ) || !IsPlayer( who ) ) { continue; }

        cost = 950;
        if ( IsDefined( level.zombie_treasure_chest_cost ) ) { cost = level.zombie_treasure_chest_cost; }
        else if ( IsDefined( self.zombie_cost ) ) { cost = self.zombie_cost; }

        if ( !IsDefined( who.score ) || who.score < cost ) { continue; }
        self.iw4m_box_last_trigger = who;
    }
}

function WatchBoxOutcome()
{
    if ( !IsDefined( self.zbarrier ) ) { return; }

    for ( ;; )
    {
        self.zbarrier waittill( "randomization_done" );
        self.iw4m_box_in_late_phase = true;

        // T7: zbarrier.weapon is a struct (set by treasure_chest_weapon_locking).
        // T6 stored a string on .weapon_string — that property doesn't exist in T7.
        weaponName = "undef";
        if ( IsDefined( self.zbarrier.weapon ) ) { weaponName = WeaponName( self.zbarrier.weapon ); }

        capturedUser = undefined;
        if ( IsDefined( self.chest_user ) && IsPlayer( self.chest_user ) )
        {
            capturedUser = self.chest_user;
        }

        self.zbarrier waittill( "box_spin_done" );

        timedOutValue = false;
        timedOutDefined = 0;
        if ( IsDefined( self.timedout ) )
        {
            timedOutDefined = 1;
            if ( self.timedout ) { timedOutValue = true; }
        }

        isTeddy = false;
        if ( IsDefined( self.iw4m_box_teddy_marker ) && self.iw4m_box_teddy_marker ) { isTeddy = true; }
        self.iw4m_box_teddy_marker = false;

        // 3-tier user resolution (snapshot → live → trigger-capture).
        user = capturedUser;
        if ( !IsDefined( user ) && IsDefined( self.chest_user ) && IsPlayer( self.chest_user ) )
        {
            user = self.chest_user;
        }
        if ( !IsDefined( user ) && timedOutDefined == 1 && IsDefined( self.iw4m_box_last_trigger ) && IsPlayer( self.iw4m_box_last_trigger ) )
        {
            user = self.iw4m_box_last_trigger;
        }

        cost = 950;
        if ( IsDefined( self.zombie_cost ) ) { cost = self.zombie_cost; }

        if ( IsDefined( user ) )
        {
            if ( isTeddy )
            {
                logprint( "GSE;ZP;" + BuildPlayerInfoString( user ) + ";box;teddy;" + cost + "\n" );
            }
            else if ( timedOutValue )
            {
                logprint( "GSE;ZP;" + BuildPlayerInfoString( user ) + ";box;pass;" + weaponName + ";" + cost + "\n" );
            }
            else
            {
                logprint( "GSE;ZP;" + BuildPlayerInfoString( user ) + ";box;take;" + weaponName + ";" + cost + "\n" );
            }
        }

        self.iw4m_box_last_trigger = undefined;
        self.iw4m_box_in_late_phase = false;
    }
}

//----------------//
//---- Traps ----//
//----------------//

function WaitForTrapActivations()
{
    wait ( 2 );

    traps = getentarray( "zombie_trap", "targetname" );
    for ( i = 0; i < traps.size; i++ ) { traps[i] thread WatchTrapActivation(); }
}

function WatchTrapActivation()
{
    trapType = "trap";
    if ( IsDefined( self.script_noteworthy ) ) { trapType = self.script_noteworthy; }

    cost = 1000;
    if ( IsDefined( self.zombie_cost ) ) { cost = self.zombie_cost; }

    for ( ;; )
    {
        while ( true )
        {
            if ( IsDefined( self._trap_in_use ) && self._trap_in_use == 1 ) { break; }
            wait ( 0.2 );
        }

        players = getplayers();
        closest = undefined;
        closestDist = 99999;

        for ( i = 0; i < players.size; i++ )
        {
            if ( !IsAlive( players[i] ) ) { continue; }
            dist = distance( players[i].origin, self.origin );
            if ( dist < closestDist )
            {
                closestDist = dist;
                closest = players[i];
            }
        }

        if ( IsDefined( closest ) )
        {
            logprint( "GSE;ZP;" + BuildPlayerInfoString( closest ) + ";trap;activate;" + trapType + ";" + cost + "\n" );
        }

        while ( true )
        {
            if ( IsDefined( self._trap_in_use ) && self._trap_in_use != 1 ) { break; }
            wait ( 1 );
        }
    }
}

//----------------//
//---- Craftables ----//
//----------------//

function WaitForCraftables()
{
    wait ( 3 );

    if ( !IsDefined( level.zombie_craftablestubs ) ) { return; }

    names = getarraykeys( level.zombie_craftablestubs );
    for ( i = 0; i < names.size; i++ )
    {
        thread WatchCraftableComplete( names[i] );
    }
}

function WatchCraftableComplete( craftableName )
{
    for ( ;; )
    {
        // T7 _zm_craftables fires `level notify(name + "_crafted", player)`.
        level waittill( craftableName + "_crafted", player );
        if ( !IsDefined( player ) || !IsPlayer( player ) ) { continue; }
        logprint( "GSE;ZP;" + BuildPlayerInfoString( player ) + ";build;complete;" + craftableName + "\n" );
    }
}

//----------------//
//---- Power ----//
//----------------//

function WatchPowerSwitches()
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
        for ( i = 0; i < ents.size; i++ ) { triggers[ triggers.size ] = ents[i]; }
    }

    for ( t = 0; t < triggers.size; t++ ) { triggers[t] thread WatchSinglePowerSwitch(); }
}

function WatchSinglePowerSwitch()
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

function WatchPowerStateChanges()
{
    level endon( "end_game" );

    while ( true )
    {
        level flag::wait_till( "power_on" );
        EmitPowerOn();

        while ( level flag::get( "power_on" ) )
        {
            wait ( 0.5 );
        }
        EmitPowerOff();
    }
}

function EmitPowerOn()
{
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
        logprint( "GSE;ZW;power;on;player;" + BuildPlayerInfoString( activator ) + "\n" );
    }
    else
    {
        logprint( "GSE;ZW;power;on;world\n" );
    }
}

function EmitPowerOff()
{
    logprint( "GSE;ZW;power;off;world\n" );
}

//----------------//
//---- Zombies Remaining ----//
//----------------//

function WatchZombiesRemaining()
{
    level endon( "end_game" );
    last_remaining = -1;
    last_alive = -1;

    while ( true )
    {
        wait ( 5 );
        if ( !IsDefined( level.zombie_total ) || !IsDefined( level.round_number ) ) { continue; }

        remaining = level.zombie_total;
        alive = zombie_utility::get_current_zombie_count();
        if ( remaining == last_remaining && alive == last_alive ) { continue; }

        logprint( "GSE;ZW;zombies;" + level.round_number + ";" + remaining + ";" + alive + "\n" );
        last_remaining = remaining;
        last_alive = alive;
    }
}

//----------------//
//---- Easter Eggs (V1 STUBS — wiring only) ----//
//----------------//

// Per-map switch preserved as a hookpoint. **Intentionally empty bodies —
// not a stub.** T7 EE completion is derived server-side from terminal-step
// emissions in WaitForT7EasterEggSteps below; the premium plugin sets
// quest.HasCanonicalNotify = false on every T7 quest, so the "all steps
// logged → mark complete" derivation path is authoritative. Same outcome
// as T4/T5/T6's canonical emission, just on a different signal channel.
//
// Why the divergence: most T7 main-quest terminal flags are hashed in the
// shiversoftdev dump (no source name), so a clean named-notify wait isn't
// always available. Step-based derivation lets us cover all 14 maps using
// the named per-step flags we DO have, without needing a canonical
// terminal flag per map. Cases where T7 DOES have a clean terminal flag
// (e.g. zm_zod's "ee_complete") are still surfaced — they appear as the
// final step in the quest's step list (t7_soe_complete), which fires the
// derivation when logged.
function WaitForEasterEggComplete()
{
    level endon( "end_game" );

    if ( !IsDefined( level.script ) )
    {
        return;
    }

    switch ( level.script )
    {
        case "zm_zod":         break; // Shadows of Evil — Apocalypse Averted
        case "zm_factory":     break; // The Giant
        case "zm_castle":      break; // Der Eisendrache — My Brother's Keeper
        case "zm_island":      break; // Zetsubou No Shima — A Better Tomorrow
        case "zm_stalingrad":  break; // Gorod Krovi — Love and War
        case "zm_genesis":     break; // Revelations — For The Good of All
        case "zm_prototype":   break; // Nacht der Untoten
        case "zm_asylum":      break; // Verruckt
        case "zm_sumpf":       break; // Shi No Numa
        case "zm_cosmodrome":  break; // Ascension — Casimir Mechanism
        case "zm_theater":     break; // Kino der Toten — no main quest
        case "zm_moon":        break; // Moon — Richtofen's Grand Scheme
        case "zm_temple":      break; // Shangri-La — Time Travel Will Tell
        case "zm_tomb":        break; // Origins — Little Lost Girl
        default:
            logprint( "[ZM-EE] No canonical EE watcher configured for map=" + level.script + "\n" );
            return;
    }
}

function WaitForT7EasterEggSteps()
{
    level endon( "end_game" );

    if ( !IsDefined( level.script ) )
    {
        return;
    }

    switch ( level.script )
    {
        case "zm_zod":
            // Shadows of Evil — THREE quests:
            //   Apocalypse Averted (main, requires 4P for full ending; C# gates
            //     UI with MinPlayers=4). 8 step flags exposed by zm_zod_ee.gsc:
            //     ee_book, totem_placed, 4 × ee_keeper_<char>_resurrected,
            //     ee_boss_defeated (phase 1 = "Apocalypse Ascendant"),
            //     ee_complete (terminal = "Apocalypse Averted").
            //   Song "Snake Skin Boots" — 3 radios → music state
            //     "snakeskinboots" or "_instr" (radio-order-dependent).
            //   Song "Cold Hard Cash" — 3 mic parts assembled → "coldhardcash".
            level thread WatchT7FlagStep( "ee_book", "t7_soe_book" );
            level thread WatchT7FlagStep( "totem_placed", "t7_soe_totem" );
            level thread WatchT7FlagStep( "ee_keeper_boxer_resurrected",     "t7_soe_keeper_boxer" );
            level thread WatchT7FlagStep( "ee_keeper_detective_resurrected", "t7_soe_keeper_detective" );
            level thread WatchT7FlagStep( "ee_keeper_femme_resurrected",     "t7_soe_keeper_femme" );
            level thread WatchT7FlagStep( "ee_keeper_magician_resurrected",  "t7_soe_keeper_magician" );
            level thread WatchT7FlagStep( "ee_boss_defeated", "t7_soe_boss_1" );
            level thread WatchT7FlagStep( "ee_complete", "t7_soe_complete" );
            level thread WatchT7MusicStateStep( "snakeskinboots",       "t7_soe_snakeskin" );
            level thread WatchT7MusicStateStep( "snakeskinboots_instr", "t7_soe_snakeskin" );
            level thread WatchT7MusicStateStep( "coldhardcash", "t7_soe_cash" );
            // Equipment-upgrade side quests — wonder-weapon-equivalent for SoE:
            //   Upgraded Riot Shield — weapon name "zod_riotshield_upgraded"
            //     bought via zm_equipment::buy at zm_zod_ee_side.gsc:1635.
            //   Upgraded Bouncing Bettys (Trip Mines) — "bouncingbetty_holly"
            //     mine type registered at zm_zod_ee_side.gsc:65.
            //   Upgraded Li'l Arnies — hash-literal terminal notify
            //     #"hash_21edb6b6" fired at zm_zod_ee_side.gsc:1481 after the
            //     stage-dance cutscene. BO3 EOL = hash frozen, safe to wait.
            level thread WatchWeaponSubstringUpgrade( "zod_riotshield_upgraded", "t7_soe_shield_upgrade" );
            level thread WatchWeaponSubstringUpgrade( "bouncingbetty_holly",     "t7_soe_betty_upgrade" );
            level thread WatchSoEArnieUpgrade();
            break;
        case "zm_factory":
            // The Giant — three EEs: song (Beauty of Annihilation Remix),
            // flytrap (Hide and Go Seek main quest), secret perk (Sixth Perk).
            // All three use HasCanonicalNotify: false on the C# side — completion
            // derives from "all steps logged" because no single notify cleanly
            // signals end-state per quest (flytrap is a 3-flag composite, secret
            // perk's snow_ee_completed flag is itself the final step we emit).
            level thread WatchFactoryMusic();
            level thread WatchFactoryFlytrap();
            level thread WatchFactorySecretPerk();
            break;
        case "zm_castle":
            // Der Eisendrache — SIX quests:
            //   My Brother's Keeper (main, MinPlayers=1 — solo canonical):
            //     8 step flags from zm_castle_ee.gsc covering pyramid →
            //     fuse → safe → key → simon → MPD canister → moon rockets → outro.
            //   Song "Dead Again" — 3 teddy bears → music state "dead_again".
            //   Song "Requiem Aeternam" (Gramophone) — 3 gramophones → "requiem".
            //   Music Box — single activation in Samantha's bedroom.
            //   Disco Inferno — moon globe shot at planetary alignment.
            //   Elemental Bow Upgrades — 4 bows (Storm/Wolf/Fire/Void).
            //     Engine flag names are hashed in the dump; detect via per-player
            //     weapon inventory poll for each elemental_bow_* substring.
            level thread WatchT7FlagStep( "ee_start_done",              "t7_de_pyramid" );
            level thread WatchT7FlagStep( "ee_fuse_placed",             "t7_de_fuse" );
            level thread WatchT7FlagStep( "ee_safe_open",               "t7_de_safe" );
            level thread WatchT7FlagStep( "ee_golden_key",              "t7_de_key" );
            level thread WatchT7FlagStep( "end_simon",                  "t7_de_simon" );
            level thread WatchT7FlagStep( "mpd_canister_replacement",   "t7_de_canister" );
            level thread WatchT7FlagStep( "sent_rockets_to_the_moon",   "t7_de_rockets" );
            level thread WatchT7FlagStep( "ee_outro",                   "t7_de_complete" );
            level thread WatchT7MusicStateStep( "dead_again", "t7_de_song_deadagain" );
            level thread WatchT7MusicStateStep( "requiem",    "t7_de_song_requiem" );
            level thread WatchT7FlagStep( "ee_music_box_turning", "t7_de_musicbox" );
            level thread WatchT7FlagStep( "ee_disco_inferno",     "t7_de_disco" );
            level thread WatchWeaponSubstringUpgrade( "elemental_bow_storm",       "t7_de_bow_storm" );
            level thread WatchWeaponSubstringUpgrade( "elemental_bow_wolf_howl",   "t7_de_bow_wolf" );
            level thread WatchWeaponSubstringUpgrade( "elemental_bow_rune_prison", "t7_de_bow_fire" );
            level thread WatchWeaponSubstringUpgrade( "elemental_bow_demongate",   "t7_de_bow_void" );
            break;
        case "zm_island":
            // Zetsubou No Shima — FIVE quests:
            //   Seeds of Doubt (main, solo-canonical):
            //     trilogy_released → 3 elevator gears → AA gun → outro.
            //   KT-4 Wonder Weapon: base ww_obtained + Masamune upgrade.
            //   Skull of Nan Sapwe: 4 altar rituals + skull obtained.
            //   Spider EE: cage charge → mom trapped → quest complete.
            //   Song "Dead Flowers": music state poll, single step.
            level thread WatchT7FlagStep( "trilogy_released",           "t7_zn_trilogy" );
            level thread WatchT7FlagStep( "elevator_part_gear1_placed", "t7_zn_gear_1" );
            level thread WatchT7FlagStep( "elevator_part_gear2_placed", "t7_zn_gear_2" );
            level thread WatchT7FlagStep( "elevator_part_gear3_placed", "t7_zn_gear_3" );
            level thread WatchT7FlagStep( "aa_gun_ee_complete",         "t7_zn_aagun" );
            level thread WatchT7FlagStep( "flag_outro_cutscene_done",   "t7_zn_complete" );
            level thread WatchT7FlagStep( "ww_obtained", "t7_zn_kt4_base" );
            level thread WatchT7FlagStep( "wwup_ready",  "t7_zn_kt4_upgrade" );
            level thread WatchT7FlagStep( "skullquest_ritual_complete1", "t7_zn_skull_ritual_1" );
            level thread WatchT7FlagStep( "skullquest_ritual_complete2", "t7_zn_skull_ritual_2" );
            level thread WatchT7FlagStep( "skullquest_ritual_complete3", "t7_zn_skull_ritual_3" );
            level thread WatchT7FlagStep( "skullquest_ritual_complete4", "t7_zn_skull_ritual_4" );
            level thread WatchT7FlagStep( "skull_quest_complete",        "t7_zn_skull_obtained" );
            level thread WatchT7FlagStep( "charged_spider_cage_powerup",            "t7_zn_spider_charge" );
            level thread WatchT7FlagStep( "spider_from_mars_trapped_in_raised_cage", "t7_zn_spider_trap" );
            level thread WatchT7FlagStep( "spider_ee_quest_complete",               "t7_zn_spider_complete" );
            level thread WatchT7MusicStateStep( "dead_flowers", "t7_zn_song" );
            break;
        case "zm_stalingrad":
            // Gorod Krovi — FOUR quests, all solo-canonical:
            //   Love and War (main): 8 steps from zm_stalingrad_ee_main.gsc +
            //     zm_stalingrad_gauntlet.gsc. Terminal is a hashed notify
            //     (#"hash_6460283a") fired inside ee_outro() — no named flag
            //     exists for "EE complete" on this map. Real call site:
            //     zm_stalingrad_nikolai.gsc:114 post-boss-defeat.
            //   Song "Dead Ended" — 3 vodka bottles → "dead_ended" state.
            //   Song "Ace of Spades" — playing cards → "ace_of_spades" state.
            //   Song "Samantha's Lullaby" — monkey bombs on dragon fires →
            //     "sam" state (NOT the snd_zhdegg_activate flag used by the
            //     Chronicles maps — Gorod Krovi uses music-state instead).
            level thread WatchT7FlagStep( "generator_on",             "t7_gk_power" );
            level thread WatchT7FlagStep( "keys_placed",              "t7_gk_keys" );
            level thread WatchT7FlagStep( "dragon_egg_acquired",      "t7_gk_egg" );
            // (gauntlet step moved to standalone "Dragon Gauntlet" quest below)
            level thread WatchT7FlagStep( "weapon_cores_delivered",   "t7_gk_cores" );
            level thread WatchT7FlagStep( "sophia_escaped",           "t7_gk_sophia" );
            level thread WatchT7FlagStep( "players_in_arena",         "t7_gk_arena" );
            level thread WatchGorodKroviComplete();
            // Siegfried's Gauntlet upgrade chain — standalone quest. Replaces
            // the prior single t7_gk_gauntlet step in Love and War (acquisition
            // → 4 upgrade steps → quest complete). Same convention as
            // zm_island KT-4/Skull being tracked separately from Seeds of Doubt.
            level thread WatchT7FlagStep( "dragon_gauntlet_acquired", "t7_gk_gauntlet_acquired" );
            level thread WatchT7FlagStep( "gauntlet_step_2_complete", "t7_gk_gauntlet_step_2" );
            level thread WatchT7FlagStep( "gauntlet_step_3_complete", "t7_gk_gauntlet_step_3" );
            level thread WatchT7FlagStep( "gauntlet_step_4_complete", "t7_gk_gauntlet_step_4" );
            level thread WatchT7FlagStep( "gauntlet_quest_complete",  "t7_gk_gauntlet_complete" );
            level thread WatchT7MusicStateStep( "dead_ended",    "t7_gk_song_deadended" );
            level thread WatchT7MusicStateStep( "ace_of_spades", "t7_gk_song_ace" );
            level thread WatchT7MusicStateStep( "sam",           "t7_gk_song_sam" );
            break;
        case "zm_genesis":
            // Revelations — TWO quests, solo-canonical:
            //   For The Good of All (main): 13 step flags spanning the
            //     character-graves opener through the Shadowman outro.
            //     Terminal "ending_room" is set by zm_genesis_arena.gsc:5489
            //     after the Shadowman defeat cutscene begins — distinct from
            //     the level's #"end_game" notify (fired at ee_quest:2414 a
            //     few seconds later, which is also our level endon).
            //   Song "The Gift": 3 teddy bears across the map → music state
            //     "the_gift" via zm_genesis_sound.gsc:517. Overlaps the main
            //     quest's grand_tour/toys_collected steps (the same teddies
            //     drive both); tracked separately via music-state poll.
            level thread WatchT7FlagStep( "character_stones_done", "t7_rv_stones" );
            level thread WatchT7FlagStep( "placed_audio3",         "t7_rv_audio" );
            level thread WatchT7FlagStep( "b_targets_collected",   "t7_rv_targets" );
            level thread WatchT7FlagStep( "acm_done",              "t7_rv_corruption" );
            level thread WatchT7FlagStep( "shards_done",           "t7_rv_shards" );
            level thread WatchT7FlagStep( "sophia_activated",      "t7_rv_sophia" );
            level thread WatchT7FlagStep( "sophia_at_teleporter",  "t7_rv_sophia_teleporter" );
            level thread WatchT7FlagStep( "book_picked_up",        "t7_rv_book" );
            level thread WatchT7FlagStep( "book_runes_success",    "t7_rv_runes" );
            level thread WatchT7FlagStep( "grand_tour",            "t7_rv_grand_tour" );
            level thread WatchT7FlagStep( "toys_collected",        "t7_rv_toys" );
            level thread WatchT7FlagStep( "boss_fight",            "t7_rv_boss" );
            level thread WatchT7FlagStep( "ending_room",           "t7_rv_complete" );
            level thread WatchT7MusicStateStep( "the_gift", "t7_rv_song" );
            level thread WatchT7FlagStep( "lil_arnie_prereq_done", "t7_rv_arnie_prereq" );
            level thread WatchT7FlagStep( "lil_arnie_done",        "t7_rv_arnie_done" );
            break;
        case "zm_prototype":
            // Nacht der Untoten — two EEs:
            //   Song "Undone" (returning W@W) — destroy all explodable barrels.
            //     Engine fires sndmusicsystem_playstate("undone") at the end of
            //     zm_prototype_barrels.gsc:289 after counting `hash_83cc4809`
            //     notifies once per barrel destroyed.
            //   Hide-and-Seek "Samantha's Lullaby" (NEW Chronicles) — 4 hidden
            //     buttons → snd_zhdegg_activate flag set by zm_prototype.gsc:659.
            //     The post-flag doll-hunt phase has no server-visible state we
            //     can hook; flag completion is our terminal.
            level thread WatchT7MusicStateStep( "undone", "t7_nu_song" );
            level thread WatchT7FlagStep( "snd_zhdegg_activate", "t7_nu_hns" );
            break;
        case "zm_asylum":
            // Verruckt — two EEs:
            //   Song "Lullaby for a Dead Man" (returning W@W) — flush the
            //     leftmost upstairs toilet 3 times. Engine plays state
            //     "lullaby_for_a_dead_man" at zm_asylum.gsc:650.
            //   Hide-and-Seek "Samantha's Sorrow" (NEW Chronicles) — 3 toilets
            //     flushed in 935 pattern (right 9 / middle 3 / left 5) →
            //     snd_zhdegg_activate set by zm_asylum.gsc:1512.
            level thread WatchT7MusicStateStep( "lullaby_for_a_dead_man", "t7_vr_song" );
            level thread WatchT7FlagStep( "snd_zhdegg_activate", "t7_vr_hns" );
            break;
        case "zm_sumpf":
            // Shi No Numa — two EEs:
            //   Song "The One" (returning W@W) — phone in Comm Room interacted
            //     4 times (community guide says "3" but engine counts 4 — final
            //     iteration plays the answer audio). State "the_one" at
            //     zm_sumpf.gsc:732.
            //   Hide-and-Seek "Samantha's Sorrow" (NEW Chronicles) — 4 metal
            //     pans in Fishing Hut shot with starting pistol → flag set by
            //     zm_sumpf.gsc:1268.
            level thread WatchT7MusicStateStep( "the_one", "t7_sh_song" );
            level thread WatchT7FlagStep( "snd_zhdegg_activate", "t7_sh_hns" );
            break;
        case "zm_cosmodrome":
            // Ascension — FOUR quests:
            //   Casimir Mechanism (main, MinPlayers=4): 7 named flag steps
            //     from zm_cosmodrome_eggs.gsc. Terminal "weapons_combined"
            //     fires when Thundergun + Ray Gun + Nesting Dolls combined
            //     damage exceeds threshold → triggers soul_release cutscene.
            //     thundergun_hit (line 1095) is a precondition gate inside
            //     step 7, not its own step.
            //   Song "Abracadavre" — 3 teddy bears → snd_song_completed →
            //     music state "abracadavre" (amb.gsc:226).
            //   Song "Not Ready to Die" (A7X) — all egg_phone targets meleed
            //     → music state "not_ready_to_die" (amb.gsc:252).
            //   Samantha's HnS (Chronicles addition) — snd_zhdegg_activate
            //     set by zm_cosmodrome_amb.gsc:563.
            level thread WatchT7FlagStep( "target_teleported",   "t7_as_gersh" );
            level thread WatchT7FlagStep( "rerouted_power",      "t7_as_monkey" );
            level thread WatchT7FlagStep( "switches_synced",     "t7_as_clock" );
            level thread WatchT7FlagStep( "pressure_sustained",  "t7_as_luna" );
            level thread WatchT7FlagStep( "letter_acquired",     "t7_as_letter" );
            level thread WatchT7FlagStep( "passkey_confirmed",   "t7_as_passkey" );
            level thread WatchT7FlagStep( "weapons_combined",    "t7_as_complete" );
            level thread WatchT7MusicStateStep( "abracadavre",      "t7_as_song_abracadavre" );
            level thread WatchT7MusicStateStep( "not_ready_to_die", "t7_as_song_nrtd" );
            level thread WatchT7FlagStep( "snd_zhdegg_activate", "t7_as_hns" );
            break;
        case "zm_theater":
            // Kino der Toten — TWO quests, no main EE (W@W/BO1 never had one,
            // Chronicles didn't add one):
            //   Song "115" — 3 meteor rocks shot → music state "115" at
            //     zm_theater_amb.gsc:224.
            //   Samantha Doll HnS (Chronicles addition) — knocking door
            //     pattern echoed → first doll → 5 hidden dolls shot →
            //     return to original. snd_zhdegg_activate flag set by
            //     zm_theater_amb.gsc:483 on terminal step.
            level thread WatchT7MusicStateStep( "115", "t7_kn_song" );
            level thread WatchT7FlagStep( "snd_zhdegg_activate", "t7_kn_hns" );
            break;
        case "zm_moon":
            // Moon — FOUR quests:
            //   Richtofen's Grand Scheme (main): combines Cryogenic Slumber
            //     Party (CSP, steps 1-4) + Big Bang Theory (BBT, steps 5-7).
            //     Chronicles solo-canonical (BO1 BBT was co-op-only;
            //     Chronicles unlocked solo).
            //     Terminal "complete_be_1" set by sq_be.gsc:501 inside the
            //     final BBT validation switch.
            //   Song "Coming Home" — 3 PES-helmet teddy bears, music state
            //     "cominghome" (amb.gsc:534).
            //   Song "Nightmare" — excavator-suicide co-op trigger, music
            //     state "nightmare" (achievement.gsc:156). Solo-impossible.
            //   Samantha's Journey HnS (Chronicles addition) — spinning
            //     Samantha doll on 115 box → snd_zhdegg_activate (amb.gsc:839).
            //   8-Bit Coming Home / 8-Bit Pareidolia minor variants skipped —
            //     dynamic state names via self.script_string not enumerable.
            level thread WatchT7FlagStep( "first_tanks_charged", "t7_mn_tanks" );
            level thread WatchT7FlagStep( "c_built",             "t7_mn_capsule" );
            level thread WatchT7FlagStep( "vg_charged",          "t7_mn_vril" );
            level thread WatchT7FlagStep( "soul_swap_done",      "t7_mn_csp" );
            level thread WatchT7FlagStep( "sam_switch_thrown",   "t7_mn_samswitch" );
            level thread WatchT7FlagStep( "be2",                 "t7_mn_bbt_mid" );
            level thread WatchT7FlagStep( "complete_be_1",       "t7_mn_complete" );
            level thread WatchT7MusicStateStep( "cominghome", "t7_mn_song_cominghome" );
            level thread WatchT7MusicStateStep( "nightmare",  "t7_mn_song_nightmare" );
            level thread WatchT7FlagStep( "snd_zhdegg_activate", "t7_mn_hns" );
            break;
        case "zm_temple":
            // Shangri-La — THREE quests:
            //   Time Travel Will Tell (main, MinPlayers=4): 5 named flag
            //     steps drawn from zm_temple_sq*.gsc files. Eclipse activation
            //     step requires 4 simultaneous button presses at Quick Revive
            //     — same 4P gate as Casimir. Terminal "meteorite_shrunk"
            //     fires when shrink-ray + dynamite chain rewards Focus Stone.
            //   Song "Pareidolia" — 3 element-115 rocks → music state
            //     "pareidolia" (amb.gsc:137).
            //   Samantha's HnS (Chronicles addition) — snd_zhdegg_activate
            //     set by zm_temple_amb.gsc:244.
            //   Intermediate flags pap_override / radio_7 / radio_9 are sub-
            //     steps covered by neighboring main steps — not tracked.
            level thread WatchT7FlagStep( "trap_destroyed",    "t7_sl_trap" );
            level thread WatchT7FlagStep( "radio_4_played",    "t7_sl_explorers" );
            level thread WatchT7FlagStep( "gongs_resonating",  "t7_sl_gongs" );
            level thread WatchT7FlagStep( "given_dynamite",    "t7_sl_dynamite" );
            level thread WatchT7FlagStep( "meteorite_shrunk",  "t7_sl_complete" );
            level thread WatchT7MusicStateStep( "pareidolia", "t7_sl_song" );
            level thread WatchT7FlagStep( "snd_zhdegg_activate", "t7_sl_hns" );
            break;
        case "zm_tomb":
            // Origins — NINE quests, solo-canonical:
            //   Little Lost Girl (main): 11 named flag steps spanning
            //     staff-craft → upgrade → place → quadrotor → mech fight →
            //     Maxis Drone → One Inch Punch → souls absorbed → portal →
            //     Samantha released (terminal).
            //     Note: ee_quadrotor_disabled is set at step_4 (first Panzer
            //     down), cleared at step_5, re-set at step_8. Our wait_till
            //     fires once on first set = step_4 timing. The final boss
            //     completion is covered by ee_samantha_released.
            //   4 Elemental Staff Upgrades — Kagutsuchi's Blood (fire),
            //     Ull's Arrow (water/ice), Boreas' Fury (air/wind), Kimat's
            //     Bite (lightning). Per-staff upgrade-unlock flags are
            //     hashed in the dump (same as zm_castle bows); detect via
            //     weapon-inventory poll on staff_<element>_upgraded.
            //   4 Song EEs — Archangel (3 records → "archangel"),
            //     Aether (3 mus115 prone-triggers → "aether"),
            //     Shepherd of Fire (3 radios → "shepherd_of_fire"),
            //     Samantha's Lullaby (Chronicles HnS — snd_zhdegg_activate
            //     set by zm_tomb_amb.gsc:635 after 4 elemental targets shot).
            level thread WatchT7FlagStep( "ee_all_staffs_crafted",        "t7_or_staffs_crafted" );
            level thread WatchT7FlagStep( "ee_all_staffs_upgraded",       "t7_or_staffs_upgraded" );
            level thread WatchT7FlagStep( "ee_all_staffs_placed",         "t7_or_staffs_placed" );
            level thread WatchT7FlagStep( "ee_mech_zombie_hole_opened",   "t7_or_mech_hole" );
            level thread WatchT7FlagStep( "ee_quadrotor_disabled",        "t7_or_quadrotor" );
            level thread WatchT7FlagStep( "ee_mech_zombie_fight_completed", "t7_or_mech_fight" );
            level thread WatchT7FlagStep( "ee_maxis_drone_retrieved",     "t7_or_drone" );
            level thread WatchT7FlagStep( "ee_all_players_upgraded_punch", "t7_or_punch" );
            level thread WatchT7FlagStep( "ee_souls_absorbed",            "t7_or_souls" );
            level thread WatchT7FlagStep( "ee_sam_portal_active",         "t7_or_portal" );
            level thread WatchT7FlagStep( "ee_samantha_released",         "t7_or_complete" );
            level thread WatchWeaponSubstringUpgrade( "staff_fire_upgraded",      "t7_or_staff_fire" );
            level thread WatchWeaponSubstringUpgrade( "staff_water_upgraded",     "t7_or_staff_ice" );
            level thread WatchWeaponSubstringUpgrade( "staff_air_upgraded",       "t7_or_staff_wind" );
            level thread WatchWeaponSubstringUpgrade( "staff_lightning_upgraded", "t7_or_staff_lightning" );
            level thread WatchT7MusicStateStep( "archangel",        "t7_or_song_archangel" );
            level thread WatchT7MusicStateStep( "aether",           "t7_or_song_aether" );
            level thread WatchT7MusicStateStep( "shepherd_of_fire", "t7_or_song_shepherd" );
            level thread WatchT7FlagStep( "snd_zhdegg_activate", "t7_or_hns" );
            break;
        default:               return;
    }

    logprint( "[ZM-EE] Per-step watchers armed for map=" + level.script + "\n" );
}

// Generic helper: wait for a level flag to fire, emit a step. Used across the
// T7 EE watchers — most steps are flag::set fires.
//
// flag::wait_till crashes ("cannot cast undefined to bool") on flags that the
// map script hasn't called flag::init() on yet. Our EE threads start at map
// init time, before the map's per-EE code arms its flags. Poll flag::exists
// until the flag is created, then wait_till as normal.
function WatchT7FlagStep( flagName, stepKey )
{
    level endon( "end_game" );
    WaitForT7FlagInit( flagName );
    level flag::wait_till( flagName );
    EmitEeStep( stepKey );
}

function WaitForT7FlagInit( flagName )
{
    level endon( "end_game" );
    while ( !( level flag::exists( flagName ) ) )
    {
        wait ( 0.5 );
    }
}

// Poll the shared zm_audio music state. Emits stepKey on the first observation
// of level.musicsystem.currentstate matching stateName. Used for song EEs where
// the only clean terminal signal is the engine calling sndmusicsystem_playstate
// (_zm_audio.gsc:1342 — sets m.currentstate to the requested state).
function WatchT7MusicStateStep( stateName, stepKey )
{
    level endon( "end_game" );
    for ( ;; )
    {
        if ( IsDefined( level.musicsystem )
          && IsDefined( level.musicsystem.currentstate )
          && level.musicsystem.currentstate == stateName )
        {
            EmitEeStep( stepKey );
            return;
        }
        wait ( 0.5 );
    }
}

// Generic per-player weapon-inventory poller. Fires stepKey when any live
// player's inventory contains a weapon name matching weaponSubstr.
//
// Used for any wonder-weapon-upgrade quest whose upgrade-complete flag is
// hashed in the decompiled dump (no source name available). Substring match
// tolerates engine variant suffixes / attachments. Current consumers:
//   - Der Eisendrache elemental bows (storm/wolf/fire/void)
//   - Origins elemental staffs (fire/ice/wind/lightning)
//   - SoE Shield + Bouncing Betty upgrades
function WatchWeaponSubstringUpgrade( weaponSubstr, stepKey )
{
    level endon( "end_game" );
    for ( ;; )
    {
        players = getplayers();
        for ( i = 0; i < players.size; i++ )
        {
            if ( !IsAlive( players[i] ) ) { continue; }
            if ( PlayerHasWeaponSubstr( players[i], weaponSubstr ) )
            {
                EmitEeStep( stepKey );
                return;
            }
        }
        wait ( 2 );
    }
}

function PlayerHasWeaponSubstr( player, weaponSubstr )
{
    weapons = player getweaponslist();
    if ( !IsDefined( weapons ) ) { return false; }
    for ( w = 0; w < weapons.size; w++ )
    {
        if ( IsDefined( weapons[w].name ) && IsSubStr( weapons[w].name, weaponSubstr ) )
        {
            return true;
        }
    }
    return false;
}

// Gorod Krovi "Love and War" terminal. zm_stalingrad_ee_main.gsc has no named
// "EE complete" flag; the only deterministic signal is the hashed level notify
// #"hash_6460283a" fired at the very top of ee_outro() (line 4413). The real
// production call site for ee_outro is zm_stalingrad_nikolai.gsc:114 — fired
// from function_a21082e5() which runs after the Mother Dragon is killed.
// BO3 is end-of-life so the compile-time hash is frozen; hash-literal waittill
// is robust.
function WatchGorodKroviComplete()
{
    level endon( "end_game" );
    level waittill( #"hash_6460283a" );
    EmitEeStep( "t7_gk_complete" );
}

// Shadows of Evil Upgraded Li'l Arnies terminal. zm_zod_ee_side.gsc:1481 fires
// level notify(#"hash_21edb6b6") at the end of function_c4842cb1, the stage-
// dance cutscene that runs after the player completes the placement chain
// (top hat / cane / bow tie + Black Lace stage). No named flag exists for
// this EE's terminal; hash-literal waittill is the only deterministic signal.
function WatchSoEArnieUpgrade()
{
    level endon( "end_game" );
    level waittill( #"hash_21edb6b6" );
    EmitEeStep( "t7_soe_arnie_upgrade" );
}

//---- Factory (The Giant) EE watchers ----//

// Music EE — "Beauty of Annihilation Remix". 3 brain jars; engine wires them
// via zm_audio::sndmusicsystem_eesetup (zm_factory.gsc:2220 → _zm_audio.gsc:1597).
// Each "use" hit increments level.sndeecount; song fires at == sndeemax.
// No per-jar notify is reachable from outside zm_audio's temp_ent scope, so
// poll the counter and emit step transitions.
function WatchFactoryMusic()
{
    level endon( "end_game" );

    while ( !IsDefined( level.sndeemax ) || level.sndeemax == 0 )
    {
        wait ( 0.5 );
    }

    lastCount = 0;
    for ( ;; )
    {
        if ( IsDefined( level.sndeecount ) && level.sndeecount > lastCount )
        {
            for ( i = lastCount + 1; i <= level.sndeecount; i++ )
            {
                EmitEeStep( "t7_fa_song_" + i );
            }
            lastCount = level.sndeecount;
            if ( lastCount >= level.sndeemax )
            {
                return;
            }
        }
        wait ( 0.25 );
    }
}

// Flytrap / Hide and Go Seek — main EE giving the Annihilator wonder weapon.
// 4 steps: panel hit with PaP'd weapon → 3 hidden targets (order undetermined,
// emit by first-fire index).
// References: zm_factory.gsc:1316 ("flytrap" flag), :1321-1323 (target flags).
function WatchFactoryFlytrap()
{
    level endon( "end_game" );

    level thread WatchT7FlagStep( "flytrap", "t7_fa_flytrap_panel" );

    level.iw4m_flytrap_target_idx = 0;
    level thread WatchFactoryFlytrapTarget( "ee_exp_monkey" );
    level thread WatchFactoryFlytrapTarget( "ee_bowie_bear" );
    level thread WatchFactoryFlytrapTarget( "ee_perk_bear" );
}

function WatchFactoryFlytrapTarget( flagName )
{
    level endon( "end_game" );
    WaitForT7FlagInit( flagName );
    level flag::wait_till( flagName );
    // GSC cooperative scheduling means inc+read is atomic between waits — no
    // tearing even if two targets fire in the same frame.
    level.iw4m_flytrap_target_idx = level.iw4m_flytrap_target_idx + 1;
    EmitEeStep( "t7_fa_flytrap_target_" + level.iw4m_flytrap_target_idx );
}

// Secret Perk (6th Perk-A-Cola). 3 cymbal-monkey-on-pad steps + the snow-melt
// reveal. References: zm_factory.gsc:2819/2837/2855 (console_*_completed),
// :2700 (snow_ee_completed).
function WatchFactorySecretPerk()
{
    level endon( "end_game" );

    level.iw4m_perk_pad_idx = 0;
    level thread WatchFactorySecretPerkPad( "console_one_completed" );
    level thread WatchFactorySecretPerkPad( "console_two_completed" );
    level thread WatchFactorySecretPerkPad( "console_three_completed" );
    level thread WatchT7FlagStep( "snow_ee_completed", "t7_fa_perk_done" );
}

function WatchFactorySecretPerkPad( flagName )
{
    level endon( "end_game" );
    WaitForT7FlagInit( flagName );
    level flag::wait_till( flagName );
    level.iw4m_perk_pad_idx = level.iw4m_perk_pad_idx + 1;
    EmitEeStep( "t7_fa_perk_pad_" + level.iw4m_perk_pad_idx );
}

function EmitEeStep( stepKey )
{
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
    logprint( "GSE;ZW;easter_egg;step;" + stepKey + "\n" );
}
