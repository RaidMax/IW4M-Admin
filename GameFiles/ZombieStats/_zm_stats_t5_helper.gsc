#include maps\_utility;
#include common_scripts\utility;
#include maps\_zombiemode_utility;

// ─────────────────────────────────────────────────────────────────
// T5 Zombie EE Test Helper — DEV USE ONLY
// ─────────────────────────────────────────────────────────────────
//
// Standalone script for testing T5 zombie easter eggs without
// grinding the Mystery Box / accumulating points the long way.
// NOT intended for production servers — gated behind a dvar that
// defaults off so dropping this into a normal install is a no-op.
//
// Activation:
//   set sv_iw4m_ee_helper 1
//
// On player spawn (each respawn) when enabled:
//   • 999,999 points
//   • All map perks
//   • Frag grenades (lethal slot)
//   • Map-appropriate wonder grenade in tactical slot
//   • Map-appropriate PaP'd wonder weapons in primary slots
//
// Hot-swap commands during a match:
//   set sv_iw4m_ee_helper_cmd <name>
// Polled every 0.5s; cleared after execution. Available names:
//   gersh    — Gersh Device          (Ascension)
//   dolls    — Matryoshka Dolls      (Call of the Dead)
//   qed      — Quantum Bomb          (Moon)
//   thunder  — PaP Thundergun        (any map)
//   raygun   — PaP Ray Gun           (any map)
//   shrink   — PaP Shrink Ray        (Shangri-La — Fractilizer)
//   wave     — PaP Wave Gun          (Moon)
//   zap      — Zap Gun Dual Wield    (Moon)
//   hacker   — Hacker tool           (Moon)
//   vr11     — V-R11 PaP             (Coast — sacrifice step)
//   wunder   — Wunderwaffe DG-2      (Coast — final reward)
//   sickle   — PaP Sickle            (Coast)
//   ammo     — Max ammo on current weapon
//   perks    — Re-give all map perks
//   points   — +999,999 points
//   loadout  — Re-issue full map loadout
//
// Examples (in-game console — `~` to open):
//   \set sv_iw4m_ee_helper_cmd gersh
//   \set sv_iw4m_ee_helper_cmd thunder
//   \set sv_iw4m_ee_helper_cmd loadout
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
    // doing anything. This way, user can flip the gate on/off mid-match
    // without restarting the map. Watchers are cheap (no work when disabled).
    thread WatchPlayerConnects();
    thread WatchCommandDvar();

    // Hook anyone already connected at script-load time. WatchPlayerConnects
    // only fires for FUTURE "connecting" notifies, and in zombies the player
    // is typically connected before our init runs.
    players = getplayers();
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
        // Stock T5 fires "spawned_player" each fresh spawn (round 1, revives,
        // post-laststand, map respawn). Stack a small delay so other spawn
        // threads (perks restore, loadout init) finish before we overwrite.
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
                players = getplayers();
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
    // Frag grenade lethal — present on every T5 map, supports almost every EE
    // step that requires "throw an explosive" (e.g. CoD waterfall, Shangri spikemores).
    self giveweapon( "frag_grenade_zm" );
    self set_player_lethal_grenade( "frag_grenade_zm" );
    self setweaponammoclip( "frag_grenade_zm", 4 );

    // NOTE: Map-specific wonder grenades (Gersh / Dolls / QED) are NOT auto-
    // given here. See the GiveGersh / GiveDolls / GiveQED comment block at the
    // bottom of this file for why — short version: their handler threads can't
    // be invoked from a generic cross-map helper. Roll the Mystery Box.
}

GiveMapWonderWeapons()
{
    switch ( level.script )
    {
        case "zombie_cosmodrome":
            // Final step needs PaP Thundergun + PaP Ray Gun + Gersh + Dolls.
            // Dolls aren't in this map's stock wonder set, but the stock script
            // accepts any 90k+ grenade splash — frag works as a fallback.
            self giveweapon( "thundergun_upgraded_zm" );
            self giveweapon( "ray_gun_upgraded_zm" );
            self switchtoweapon( "thundergun_upgraded_zm" );
            break;
        case "zombie_coast":
            self giveweapon( "ray_gun_upgraded_zm" );
            self giveweapon( "thundergun_upgraded_zm" );
            self switchtoweapon( "ray_gun_upgraded_zm" );
            break;
        case "zombie_temple":
            // Step 4 (Slide Crystal) needs Shrink Ray (31-79 JGb215).
            // Step 9 needs PaP Shrink Ray = Fractilizer.
            self giveweapon( "shrink_ray_upgraded_zm" );
            self giveweapon( "ray_gun_upgraded_zm" );
            self switchtoweapon( "shrink_ray_upgraded_zm" );
            break;
        case "zombie_moon":
            // Wave Gun + Zap Gun for general carry; QED tactical is set above.
            self giveweapon( "microwavegundw_upgraded_zm" );
            self giveweapon( "ray_gun_upgraded_zm" );
            self switchtoweapon( "microwavegundw_upgraded_zm" );
            break;
        default:
            // Theater / pentagon — just a strong baseline.
            self giveweapon( "ray_gun_upgraded_zm" );
            self switchtoweapon( "ray_gun_upgraded_zm" );
            break;
    }
}

GiveAllPerks()
{
    // Mirror Moon SQ reward pattern — derive perk list from vending machines
    // present on the current map rather than hard-coding (handles map variants).
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
            self maps\_zombiemode_perks::give_perk( level._iw4m_helper_perks[i] );
            wait ( 0.1 );
        }
    }
}

RunHelperCommand( cmd )
{
    self endon( "disconnect" );

    switch ( cmd )
    {
        case "gersh":   self GiveGersh();                                 break;
        case "dolls":   self GiveDolls();                                 break;
        case "qed":     self GiveQED();                                   break;
        case "thunder": self GivePrimary(  "thundergun_upgraded_zm" );    break;
        case "raygun":  self GivePrimary(  "ray_gun_upgraded_zm" );       break;
        case "shrink":  self GivePrimary(  "shrink_ray_upgraded_zm" );    break;
        case "wave":    self GivePrimary(  "microwavegundw_upgraded_zm" );break;
        case "zap":     self GivePrimary(  "zapgundw_upgraded_zm" );      break;
        case "hacker":  self GivePrimary(  "hacker_zm" );                 break;
        case "vr11":    self GivePrimary(  "humangun_upgraded_zm" );      break;
        case "wunder":  self GivePrimary(  "tesla_gun_upgraded_zm" );     break;
        case "sickle":  self GivePrimary(  "bowie_knife_zm" );            break;
        case "ammo":    self MaxAmmoCurrent();                            break;
        case "perks":   self GiveAllPerks();                              break;
        case "points":  self.score = self.score + 999999;                 break;
        case "loadout": self GiveLoadout();                               break;

        // ── EE-step force-fire shortcuts ──
        // Skip the gameplay action and directly set the flag the stock script
        // is waiting on. Each invocation:
        //   1. Fires our WatchT5Flag watcher → confirms our hook works
        //   2. Unblocks the next step's setup in stock script (since each EE
        //      step's init is gated on the prior step's flag)
        // Map-specific — only the current map's flags are valid; others no-op.
        case "ee1": ForceFlag( "target_teleported",  "Ascension Step 1" );      break;
        case "ee2": ForceFlag( "rerouted_power",     "Ascension Step 2" );      break;
        case "ee3": ForceFlag( "switches_synced",    "Ascension Step 3" );      break;
        case "ee4": ForceFlag( "pressure_sustained", "Ascension Step 4" );      break;
        case "ee5": ForceFlag( "passkey_confirmed",  "Ascension Step 5" );      break;
        case "ee6": ForceFlag( "weapons_combined",   "Ascension Step 6 (final)" ); break;

        // Increment counter-based song quests (Ascension teddy bears /
        // Coast + Shangri + Moon meteor counter). One step per invocation.
        case "song": ForceCounter( "Song step" ); break;

        default:
            self iprintln( "[EE Helper] Unknown cmd: " + cmd );
            return;
    }

    self iprintln( "[EE Helper] " + cmd );
}

ForceFlag( flagName, description )
{
    if ( !IsDefined( level.flag ) || !IsDefined( level.flag[ flagName ] ) )
    {
        self iprintln( "[EE Helper] Flag '" + flagName + "' not initialized on this map (" + description + " is map-specific)" );
        return;
    }
    if ( level.flag[ flagName ] )
    {
        self iprintln( "[EE Helper] " + description + " already set" );
        return;
    }
    flag_set( flagName );
    self iprintln( "[EE Helper] Set " + description + " (flag: " + flagName + ")" );
}

ForceCounter( description )
{
    // Ascension uses teddybear_counter; Coast/Shangri/Moon use meteor_counter.
    // Bump whichever applies on this map (both can coexist if some custom map
    // wires both — fine, just bumps each).
    bumped = false;
    if ( IsDefined( level.teddybear_counter ) )
    {
        level.teddybear_counter = level.teddybear_counter + 1;
        self iprintln( "[EE Helper] teddybear_counter -> " + level.teddybear_counter );
        bumped = true;
    }
    if ( IsDefined( level.meteor_counter ) )
    {
        level.meteor_counter = level.meteor_counter + 1;
        self iprintln( "[EE Helper] meteor_counter -> " + level.meteor_counter );
        bumped = true;
    }
    if ( !bumped )
    {
        self iprintln( "[EE Helper] No song counter on this map" );
    }
}

// Wonder-grenade "givers" — DEGRADED. The helper cannot give Gersh / Dolls /
// QED properly without invoking the per-player handler thread, but:
//   1. Direct `maps\_zombiemode_weap_*::player_give_*()` won't compile on maps
//      that don't bundle the weapon script (server shutdown).
//   2. The stock `level.zombiemode_devgui_<name>_give` function pointers are
//      wrapped in /# #/ debug blocks and stripped from Pluto T5 production.
//   3. Pluto T5 has no `getfunction()` runtime resolver.
//
// Workaround for testing: use the Mystery Box. Roll until you get the wonder
// grenade — the box pickup pipeline goes through the proper player_give path
// and starts the handler thread. The helper gives you 999,999 points which
// covers many box rolls. Box weapon, throw, normal black hole / dolls / QED
// behaviour.
//
// If you find a Pluto T5 console command that gives wonder weapons properly
// (some forks add one), use that — these helper paths just print a notice.
GiveGersh() { NotifyGrenadeUnavailable( "Gersh" ); }
GiveDolls() { NotifyGrenadeUnavailable( "Matryoshka Dolls" ); }
GiveQED()   { NotifyGrenadeUnavailable( "QED" ); }

NotifyGrenadeUnavailable( name )
{
    self iprintln( "[EE Helper] " + name + " — roll Mystery Box (handler thread can't be invoked from this helper)" );
}

// Plain tactical-slot setter (no handler thread). Suitable for stock tacticals
// that don't need bespoke logic — currently unused, kept as a utility.
GiveTactical( weaponName )
{
    self giveweapon( weaponName );
    self set_player_tactical_grenade( weaponName );
    self setweaponammoclip( weaponName, 4 );
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
        case "zombie_theater":    return "Kino";
        case "zombie_pentagon":   return "Five";
        case "zombie_cosmodrome": return "Ascension";
        case "zombie_coast":      return "Call of the Dead";
        case "zombie_temple":     return "Shangri-La";
        case "zombie_moon":       return "Moon";
        default:                  return level.script;
    }
}
