#include maps\mp\_utility;
#include common_scripts\utility;
#include maps\mp\zombies\_zm_utility;

// ─────────────────────────────────────────────────────────────────
// T6 Zombie EE Test Helper — DEV USE ONLY
// ─────────────────────────────────────────────────────────────────
//
// Standalone script for testing T6 zombie easter eggs without
// grinding through the full questline. NOT intended for production
// servers — gated behind a dvar that defaults off so dropping this
// into a normal install is a no-op.
//
// Activation:
//   set sv_iw4m_ee_helper 1
//
// On player spawn (each respawn) when enabled:
//   • 999,999 points
//   • All map perks
//   • Frag grenades (lethal slot)
//   • Map-appropriate strong baseline weapon
//
// Hot-swap commands during a match (set sv_iw4m_ee_helper_cmd <name>):
//   raygun     — PaP Ray Gun
//   raygun2    — PaP Ray Gun Mark II
//   ammo       — Max ammo on current weapon
//   perks      — Re-give all map perks
//   points     — +999,999 points
//   loadout    — Re-issue full map loadout
//   power_on   — Set level "power_on" flag (skip Power Station trip)
//   power_off  — Clear "power_on" flag (Maxis path requires power off)
//
//   ── zm_buried Mined Games step force-fires ──
//   ee_br_path_maxis / ee_br_path_rich — set the bt-stage path flag (must
//      be called BEFORE any ee_br_<a-h> so the watcher knows which variant
//      to emit; the IW4M watcher logs "no path flag set" if missing).
//   ee_br_a..h     — fire each shared-stage notify (a=bt, b=mta, c=gl, d=ftl,
//                    e=ll, f=ctw+wisp_success, g=ip, h=ows).
//   ee_br_maxis_done / ee_br_rich_done — fire per-side terminal notify.
//
//   ── zm_tomb (Origins) Little Lost Girl step force-fires ──
//   ee_or_llg_<1-8>          — fire each main quest step's _over notify.
//   song_meteor / song_radio / song_aether — bump each song counter (3 = song).
//
//   ── zm_prison (Mob of the Dead) Pop Goes the Weasel step force-fires ──
//   ee_md_cycle    — set quest_completed_thrice flag (3 plane cycles done)
//   ee_md_blunder  — set warden_blundergat_obtained flag (5 skulls)
//   ee_md_spoon    — set spoon_obtained flag (Golden Spork)
//   ee_md_codes    — fire all 4 nixie_final_<n> notifies
//   ee_md_logs     — spawn+delete level.m_headphones (audio drops sequence)
//   ee_md_plane    — set plane_boarded flag
//   ee_md_voltage_115 / _935 — fire voltage song nixie notifies
//   song           — bumps level.meteor_counter (Rusty Cage bottles).
//
//   ── zm_transit ToB step force-fires ──
//   Each writes the stock-script tracking dict directly. The matching
//   GSC watcher (WatchT6SqProgress in _zm_stats_t6.gsc) sees the flip
//   and emits the [ZM-EE] step + GSE;EE;step;... pair. C# side then
//   applies the hard-lock: first variant to fire claims the match.
//   Sibling variant force-fires after lock are rejected with WRN.
//
//     ee_maxis_a      — Maxis stage A (turbines under pylon)
//     ee_maxis_b      — Maxis stage B (EMP Avogadro)
//     ee_maxis_c      — Maxis stage C (turbines at street lamps)
//     ee_maxis_done   — Maxis FINISHED (terminal)
//     ee_rich_a       — Richtofen stage A (jet gun overheats)
//     ee_rich_b       — Richtofen stage B (25 explosive kills)
//     ee_rich_c       — Richtofen stage C (4 EMPs at green lamps)
//     ee_rich_done    — Richtofen FINISHED (terminal)
//
//   ── Song bears ──
//     song            — Bump level.meteor_counter by 1 (3 needed for music)
//
// Examples (in-game console — `~` to open):
//   \set sv_iw4m_ee_helper_cmd power_on
//   \set sv_iw4m_ee_helper_cmd ee_rich_a
//   \set sv_iw4m_ee_helper_cmd song
//
// Or via RCon from outside the game.
// ─────────────────────────────────────────────────────────────────

init()
{
    // Pre-register both dvars so they exist + are settable from console even
    // before the user explicitly enables the helper.
    setdvar( "sv_iw4m_ee_helper",     "0" );
    setdvar( "sv_iw4m_ee_helper_cmd", "" );

    logprint( "[ZM-EE-HELPER] Loaded on map=" + level.script + ". Set sv_iw4m_ee_helper 1 to activate.\n" );

    // Watchers run regardless of the gate — they each check the dvar before
    // doing anything. Lets the user flip the gate on/off mid-match without
    // restarting the map. Watchers are cheap (no work when disabled).
    thread WatchPlayerConnects();
    thread WatchCommandDvar();

    // Hook anyone already connected at script-load time. WatchPlayerConnects
    // only fires for FUTURE "connecting" notifies, and in zombies the player
    // is typically connected before our init runs.
    players = get_players();
    for ( i = 0; i < players.size; i++ )
    {
        players[i] thread WatchPlayerSpawn();
    }
}

WatchPlayerConnects()
{
    level endon( "end_game" );

    for ( ;; )
    {
        level waittill( "connecting", player );
        player thread WatchPlayerSpawn();
    }
}

WatchPlayerSpawn()
{
    self endon( "disconnect" );

    for ( ;; )
    {
        // T6 fires "spawned_player" each fresh spawn. Stack a small delay so
        // other spawn threads (perks restore, loadout init) finish before we
        // overwrite.
        self waittill( "spawned_player" );
        wait ( 0.5 );

        if ( GetDvarInt( "sv_iw4m_ee_helper" ) != 1 ) { continue; }
        self GiveLoadout();
    }
}

WatchCommandDvar()
{
    level endon( "end_game" );

    // Poll cadence: 0.5s is responsive enough for testing without burning CPU.
    // First-player-only target — avoids surprise weapon swaps for other testers.
    while ( true )
    {
        cmd = GetDvar( "sv_iw4m_ee_helper_cmd" );
        if ( IsDefined( cmd ) && cmd != "" )
        {
            // Always clear the cmd dvar so a stale value doesn't loop, even
            // when the helper is disabled (otherwise enabling it later would
            // immediately re-fire whatever was last typed).
            setdvar( "sv_iw4m_ee_helper_cmd", "" );

            if ( GetDvarInt( "sv_iw4m_ee_helper" ) != 1 )
            {
                logprint( "[ZM-EE-HELPER] Ignored cmd '" + cmd + "' — sv_iw4m_ee_helper is 0\n" );
            }
            else
            {
                players = get_players();
                if ( players.size > 0 )
                {
                    logprint( "[ZM-EE-HELPER] Running cmd: " + cmd + "\n" );
                    players[0] thread RunHelperCommand( cmd );
                }
                else
                {
                    logprint( "[ZM-EE-HELPER] Ignored cmd '" + cmd + "' — no players connected\n" );
                }
            }
        }
        wait ( 0.5 );
    }
}

GiveLoadout()
{
    self endon( "disconnect" );

    self.score = 999999;
    self GiveAllPerks();
    self GiveStandardKit();
    self GiveMapWonderWeapons();

    self iprintln( "[EE Helper] Loadout issued for " + MapDisplayName() + "." );
}

GiveStandardKit()
{
    // Frag grenade lethal — present on every T6 map.
    self giveweapon( "frag_grenade_zm" );
    self set_player_lethal_grenade( "frag_grenade_zm" );
    self setweaponammoclip( "frag_grenade_zm", 4 );
}

GiveMapWonderWeapons()
{
    switch ( level.script )
    {
        case "zm_transit":
            // Strong baseline — Ray Gun. Map-specific wonder items (Jet Gun /
            // EMP grenades) are buildable / box-only and need their stock
            // handler threads, so we don't try to short-circuit them here.
            self giveweapon( "ray_gun_upgraded_zm" );
            self switchtoweapon( "ray_gun_upgraded_zm" );
            break;
        default:
            self giveweapon( "ray_gun_upgraded_zm" );
            self switchtoweapon( "ray_gun_upgraded_zm" );
            break;
    }
}

GiveAllPerks()
{
    // Mirror T5 helper pattern — derive perk list from vending machines on the
    // current map rather than hard-coding (handles map variants).
    if ( !IsDefined( level._iw4m_helper_perks ) )
    {
        level._iw4m_helper_perks = [];
        machines = GetEntArray( "zombie_vending", "targetname" );
        for ( i = 0; i < machines.size; i++ )
        {
            if ( IsDefined( machines[i].script_noteworthy ) )
            {
                level._iw4m_helper_perks[level._iw4m_helper_perks.size] = machines[i].script_noteworthy;
            }
        }
    }

    for ( i = 0; i < level._iw4m_helper_perks.size; i++ )
    {
        if ( !self HasPerk( level._iw4m_helper_perks[i] ) )
        {
            self maps\mp\zombies\_zm_perks::give_perk( level._iw4m_helper_perks[i] );
            wait ( 0.1 );
        }
    }
}

RunHelperCommand( cmd )
{
    self endon( "disconnect" );

    switch ( cmd )
    {
        case "raygun":  self GivePrimary(  "ray_gun_upgraded_zm" );       break;
        case "raygun2": self GivePrimary(  "ray_gun_mark2_upgraded_zm" ); break;
        case "ammo":    self MaxAmmoCurrent();                            break;
        case "perks":   self GiveAllPerks();                              break;
        case "points":  self.score = self.score + 999999;                 break;
        case "loadout": self GiveLoadout();                               break;

        // Power on/off — TranZit's branching gate. Maxis path needs power off,
        // Richtofen path needs power on. Lets you test both without trekking
        // to the Power Station.
        case "power_on":   self ForcePower( 1 ); break;
        case "power_off":  self ForcePower( 0 ); break;

        // ── zm_transit Tower of Babble force-fires ──
        // Each writes level.sq_progress[group][key] = 1 directly. The matching
        // WatchT6SqProgress watcher in _zm_stats_t6.gsc fires the step. WARNING:
        // setting these out of order will confuse stock script's stage gating
        // (later stages check earlier ones) — fine for testing the C# pipeline
        // but may break in-game gameplay flow. Use ee_<path>_done to skip
        // straight to the terminal.
        case "ee_maxis_a":    self ForceSqProgress( "maxis", "A_complete", "Maxis stage A" );      break;
        case "ee_maxis_b":    self ForceSqProgress( "maxis", "B_complete", "Maxis stage B" );      break;
        case "ee_maxis_c":    self ForceSqProgress( "maxis", "C_complete", "Maxis stage C" );      break;
        case "ee_maxis_done": self ForceSqProgress( "maxis", "FINISHED",   "Maxis FINISHED" );     break;
        case "ee_rich_a":     self ForceSqProgress( "rich",  "A_complete", "Richtofen stage A" );  break;
        case "ee_rich_b":     self ForceSqProgress( "rich",  "B_complete", "Richtofen stage B" );  break;
        case "ee_rich_c":     self ForceSqProgress( "rich",  "C_complete", "Richtofen stage C" );  break;
        case "ee_rich_done":  self ForceSqProgress( "rich",  "FINISHED",   "Richtofen FINISHED" ); break;

        // ── zm_highrise High Maintenance step force-fires ──
        // Die Rise uses the stock _zombiemode_sidequests stage framework.
        // Stages fire "<questId>_<stageId>_over" notifies; force-firing here
        // bypasses the gameplay action and exercises the IW4M watcher directly.
        // For the terminal we set the flags the watcher polls.
        case "ee_dr_maxis_a":    level notify( "sq_2_ssp_2_over" );
                                  self iprintln( "[EE Helper] Maxis stage A fired (sq_2_ssp_2_over)" );      break;
        case "ee_dr_maxis_b":    level notify( "sq_2_pts_2_over" );
                                  self iprintln( "[EE Helper] Maxis stage B fired (sq_2_pts_2_over)" );      break;
        case "ee_dr_maxis_done": self ForceHighriseTerminal( "max" );                                         break;
        case "ee_dr_rich_a":     level notify( "sq_1_ssp_1_over" );
                                  self iprintln( "[EE Helper] Richtofen stage A fired (sq_1_ssp_1_over)" );  break;
        case "ee_dr_rich_b":     level notify( "sq_1_pts_1_over" );
                                  self iprintln( "[EE Helper] Richtofen stage B fired (sq_1_pts_1_over)" );  break;
        case "ee_dr_rich_done":  self ForceHighriseTerminal( "rich" );                                        break;

        // ── zm_buried Mined Games force-fires ──
        // Buried's branching is determined by the bt-stage path flag. Set the
        // path first via ee_br_path_<x>, then fire stages with ee_br_<a-h>.
        // Each stage notify routes to the active variant via WatchT6BuriedStages.
        // Terminal sets the explicit per-side completion notify.
        case "ee_br_path_maxis":
            self ForceBuriedPath( "max" );
            break;
        case "ee_br_path_rich":
            self ForceBuriedPath( "rich" );
            break;
        case "ee_br_a":          level notify( "sq_bt_over" );  self iprintln( "[EE Helper] Buried stage a (sq_bt_over)" );  break;
        case "ee_br_b":          level notify( "sq_mta_over" ); self iprintln( "[EE Helper] Buried stage b (sq_mta_over)" ); break;
        case "ee_br_c":          level notify( "sq_gl_over" );  self iprintln( "[EE Helper] Buried stage c (sq_gl_over)" );  break;
        case "ee_br_d":          level notify( "sq_ftl_over" ); self iprintln( "[EE Helper] Buried stage d (sq_ftl_over)" ); break;
        case "ee_br_e":          level notify( "sq_ll_over" );  self iprintln( "[EE Helper] Buried stage e (sq_ll_over)" );  break;
        case "ee_br_f":          self ForceBuriedWisp();                                                                       break;
        case "ee_br_g":          level notify( "sq_ip_over" );  self iprintln( "[EE Helper] Buried stage g (sq_ip_over)" );  break;
        case "ee_br_h":          level notify( "sq_ows_over" ); self iprintln( "[EE Helper] Buried stage h (sq_ows_over)" ); break;
        case "ee_br_maxis_done": level notify( "sq_maxis_complete" );     self iprintln( "[EE Helper] Buried Maxis FINISHED" );     break;
        case "ee_br_rich_done":  level notify( "sq_richtofen_complete" ); self iprintln( "[EE Helper] Buried Richtofen FINISHED" ); break;

        // ── zm_tomb (Origins) Little Lost Girl step force-fires ──
        // Each fires the per-stage notify the IW4M watcher is waiting on.
        // Step 1 needs all 4 staffs upgraded + all 6 generators on first;
        // power_all_gens helps with the latter, staffs are buildable so use
        // give_all_staffs to short-circuit them (cosmetic — won't trigger
        // the actual ee_all_staffs_crafted/upgraded flag setting in stock).
        case "ee_or_llg_1":      level notify( "little_girl_lost_step_1_over" ); self iprintln( "[EE Helper] LLG step 1" ); break;
        case "ee_or_llg_2":      level notify( "little_girl_lost_step_2_over" ); self iprintln( "[EE Helper] LLG step 2" ); break;
        case "ee_or_llg_3":      level notify( "little_girl_lost_step_3_over" ); self iprintln( "[EE Helper] LLG step 3" ); break;
        case "ee_or_llg_4":      level notify( "little_girl_lost_step_4_over" ); self iprintln( "[EE Helper] LLG step 4" ); break;
        case "ee_or_llg_5":      level notify( "little_girl_lost_step_5_over" ); self iprintln( "[EE Helper] LLG step 5" ); break;
        case "ee_or_llg_6":      level notify( "little_girl_lost_step_6_over" ); self iprintln( "[EE Helper] LLG step 6" ); break;
        case "ee_or_llg_7":      level notify( "little_girl_lost_step_7_over" ); self iprintln( "[EE Helper] LLG step 7" ); break;
        case "ee_or_llg_8":      level notify( "little_girl_lost_step_8_over" ); self iprintln( "[EE Helper] LLG step 8" ); break;

        // Origins-specific song counters.
        case "song_meteor":      ForceLevelCounter( "meteor_counter",       "Archangel meteor" );        break;
        case "song_radio":       ForceLevelCounter( "found_ee_radio_count", "Shepherd of Fire radio" );  break;
        case "song_aether":      ForceLevelCounter( "snd115count",          "Aether 115 number" );       break;

        // ── zm_prison (Mob of the Dead) Pop Goes the Weasel force-fires ──
        // Each prereq sets its stock flag directly. Codes/logs/plane fire
        // their respective notifies/flags. Songs use generic helpers.
        case "ee_md_cycle":      self ForcePrisonFlag( "quest_completed_thrice",    "PGW cycle x3" );      break;
        case "ee_md_blunder":    self ForcePrisonFlag( "warden_blundergat_obtained","PGW Blundergat" );    break;
        case "ee_md_spoon":      self ForcePrisonFlag( "spoon_obtained",            "PGW Spoon" );         break;
        case "ee_md_codes":      level notify( "nixie_final_386" );
                                 level notify( "nixie_final_481" );
                                 level notify( "nixie_final_101" );
                                 level notify( "nixie_final_872" );
                                 self iprintln( "[EE Helper] PGW codes (all 4 nixie_final_*)" );           break;
        case "ee_md_logs":       self ForcePrisonAudioLogs();                                              break;
        case "ee_md_plane":      self ForcePrisonFlag( "plane_boarded",             "PGW plane" );         break;
        // Voltage song nixies (115/935 different from main quest's mobster codes).
        case "ee_md_voltage_115": level notify( "nixie_115" ); self iprintln( "[EE Helper] Voltage 115" ); break;
        case "ee_md_voltage_935": level notify( "nixie_935" ); self iprintln( "[EE Helper] Voltage 935" ); break;
        // Rusty Cage bottles share the level.meteor_counter — use existing 'song' cmd.

        // Increment counter-based song quest. One step per invocation; 3 fires song.
        case "song": ForceCounter( "Song step" ); break;

        default:
            self iprintln( "[EE Helper] Unknown cmd: " + cmd );
            return;
    }

    self iprintln( "[EE Helper] " + cmd );
}

ForcePower( on )
{
    if ( !IsDefined( level.flag ) || !IsDefined( level.flag[ "power_on" ] ) )
    {
        self iprintln( "[EE Helper] 'power_on' flag not initialized on this map" );
        return;
    }

    isOn = level.flag[ "power_on" ];
    if ( on == 1 )
    {
        if ( isOn ) { self iprintln( "[EE Helper] Power already ON" ); return; }
        flag_set( "power_on" );
        self iprintln( "[EE Helper] Power -> ON" );
    }
    else
    {
        if ( !isOn ) { self iprintln( "[EE Helper] Power already OFF" ); return; }
        flag_clear( "power_on" );
        // T6 transit_sq listens for "power_turned_off" notify — fire it so the
        // Maxis sidequest thread starts/resumes properly.
        level notify( "power_turned_off" );
        self iprintln( "[EE Helper] Power -> OFF" );
    }
}

// Generic level-counter bumper — used for Origins where 3 song EEs use distinct
// counter vars (meteor_counter / found_ee_radio_count / snd115count). GSC can't
// dynamically read level[<varName>] so we hardcode each path here. Match what
// stock script does at each interaction site (++ on the relevant counter).
ForceLevelCounter( counterName, description )
{
    if ( counterName == "meteor_counter" )
    {
        if ( !IsDefined( level.meteor_counter ) )      { self iprintln( "[EE Helper] meteor_counter not initialized" );      return; }
        level.meteor_counter = level.meteor_counter + 1;
        self iprintln( "[EE Helper] " + description + " -> " + level.meteor_counter );
    }
    else if ( counterName == "found_ee_radio_count" )
    {
        if ( !IsDefined( level.found_ee_radio_count ) ) { self iprintln( "[EE Helper] found_ee_radio_count not initialized" ); return; }
        level.found_ee_radio_count = level.found_ee_radio_count + 1;
        self iprintln( "[EE Helper] " + description + " -> " + level.found_ee_radio_count );
    }
    else if ( counterName == "snd115count" )
    {
        if ( !IsDefined( level.snd115count ) )         { self iprintln( "[EE Helper] snd115count not initialized" );         return; }
        level.snd115count = level.snd115count + 1;
        self iprintln( "[EE Helper] " + description + " -> " + level.snd115count );
    }
}

// Generic flag setter for Mob — defends against the flag not being initialized.
ForcePrisonFlag( flagName, description )
{
    if ( !IsDefined( level.flag ) || !IsDefined( level.flag[ flagName ] ) )
    {
        self iprintln( "[EE Helper] '" + flagName + "' not initialized (" + description + ")" );
        return;
    }
    if ( flag( flagName ) )
    {
        self iprintln( "[EE Helper] " + description + " already set" );
        return;
    }
    flag_set( flagName );
    self iprintln( "[EE Helper] Set " + description + " (" + flagName + ")" );
}

// Force-fires the audio-log step by spawning a placeholder m_headphones, then
// deleting it. The IW4M watcher polls IsDefined(level.m_headphones) for both
// the create and delete transitions, so we have to satisfy both.
ForcePrisonAudioLogs()
{
    if ( !IsDefined( level.m_headphones ) )
    {
        level.m_headphones = spawn( "script_origin", ( 0, 0, 0 ) );
    }
    wait ( 0.2 );  // give watcher's create-poll a chance to flip
    if ( IsDefined( level.m_headphones ) )
    {
        level.m_headphones delete();
        level.m_headphones = undefined;
    }
    self iprintln( "[EE Helper] PGW audio logs (m_headphones create+delete)" );
}

// Buried path force — sets the bt-stage flag that the IW4M watcher inspects
// to determine variant identity. Must be called before firing any subsequent
// ee_br_* stage notify, otherwise the watcher logs "no path flag set" + drops
// the step. Use ee_br_path_maxis or ee_br_path_rich.
ForceBuriedPath( whichPath )
{
    if ( !IsDefined( level.flag )
      || !IsDefined( level.flag[ "sq_is_max_tower_built" ] )
      || !IsDefined( level.flag[ "sq_is_ric_tower_built" ] ) )
    {
        self iprintln( "[EE Helper] Buried path flags not initialized — wait until the sidequest init runs" );
        return;
    }

    if ( whichPath == "max" )
    {
        if ( !flag( "sq_is_max_tower_built" ) ) { flag_set( "sq_is_max_tower_built" ); }
        if (  flag( "sq_is_ric_tower_built" ) ) { flag_clear( "sq_is_ric_tower_built" ); }
        self iprintln( "[EE Helper] Buried path -> Maxis (gallows)" );
    }
    else
    {
        if ( !flag( "sq_is_ric_tower_built" ) ) { flag_set( "sq_is_ric_tower_built" ); }
        if (  flag( "sq_is_max_tower_built" ) ) { flag_clear( "sq_is_max_tower_built" ); }
        self iprintln( "[EE Helper] Buried path -> Richtofen (guillotine)" );
    }
}

// Buried wisp/decipher stage f — the IW4M watcher only emits step f when
// sq_wisp_success is set AT THE TIME of sq_ctw_over. Set the success flag
// first then fire ctw_over.
ForceBuriedWisp()
{
    if ( !IsDefined( level.flag ) || !IsDefined( level.flag[ "sq_wisp_success" ] ) )
    {
        self iprintln( "[EE Helper] Buried sq_wisp_success flag not initialized" );
        return;
    }
    if ( !flag( "sq_wisp_success" ) ) { flag_set( "sq_wisp_success" ); }
    level notify( "sq_ctw_over" );
    self iprintln( "[EE Helper] Buried stage f (sq_wisp_success + sq_ctw_over)" );
}

// Die Rise terminal force-fire — sets the path-claim flag (sq_<x>_tower_complete)
// and the trigger flag (sq_tower_active) the watcher polls. Order matters: claim
// flag first so WatchT6HighriseTerminal sees the right path.
ForceHighriseTerminal( whichPath )
{
    if ( !IsDefined( level.flag )
      || !IsDefined( level.flag[ "sq_tower_active" ] )
      || !IsDefined( level.flag[ "sq_ric_tower_complete" ] )
      || !IsDefined( level.flag[ "sq_max_tower_complete" ] ) )
    {
        self iprintln( "[EE Helper] Die Rise tower flags not initialized — wait until power on + nav table built" );
        return;
    }

    if ( whichPath == "max" )
    {
        if ( !flag( "sq_max_tower_complete" ) ) { flag_set( "sq_max_tower_complete" ); }
    }
    else
    {
        if ( !flag( "sq_ric_tower_complete" ) ) { flag_set( "sq_ric_tower_complete" ); }
    }

    if ( !flag( "sq_tower_active" ) ) { flag_set( "sq_tower_active" ); }
    self iprintln( "[EE Helper] Die Rise terminal fired (" + whichPath + " path)" );
}

ForceSqProgress( group, key, description )
{
    // sq_progress is built inside sidequest_init_tracker after the
    // start_zombie_round_logic flag fires. Defend against race where the
    // helper fires before that init runs.
    if ( !IsDefined( level.sq_progress )
      || !IsDefined( level.sq_progress[ group ] )
      || !IsDefined( level.sq_progress[ group ][ key ] ) )
    {
        self iprintln( "[EE Helper] sq_progress[" + group + "][" + key + "] not initialized — wait until round 1 starts" );
        return;
    }
    if ( level.sq_progress[ group ][ key ] == 1 )
    {
        self iprintln( "[EE Helper] " + description + " already set" );
        return;
    }
    level.sq_progress[ group ][ key ] = 1;
    self iprintln( "[EE Helper] Set " + description + " (sq_progress[" + group + "][" + key + "])" );
}

ForceCounter( description )
{
    // T6 zm_transit uses level.meteor_counter (set up in zm_transit.gsc::
    // sndsetupmusiceasteregg). Same pattern as T5 song bears — bump by 1
    // per invocation; song fires at counter==3.
    if ( !IsDefined( level.meteor_counter ) )
    {
        self iprintln( "[EE Helper] meteor_counter not initialized on this map" );
        return;
    }
    level.meteor_counter = level.meteor_counter + 1;
    self iprintln( "[EE Helper] meteor_counter -> " + level.meteor_counter );
}

GivePrimary( weaponName )
{
    self giveweapon( weaponName );
    self switchtoweapon( weaponName );
    self GiveMaxAmmo( weaponName );
}

MaxAmmoCurrent()
{
    weap = self getcurrentweapon();
    if ( IsDefined( weap ) && weap != "none" )
    {
        self GiveMaxAmmo( weap );
    }
}

MapDisplayName()
{
    switch ( level.script )
    {
        case "zm_transit":  return "TranZit";
        case "zm_highrise": return "Die Rise";
        case "zm_buried":   return "Buried";
        case "zm_prison":   return "Mob of the Dead";
        case "zm_tomb":     return "Origins";
        case "zm_nuked":    return "Nuketown Zombies";
        default:            return level.script;
    }
}
