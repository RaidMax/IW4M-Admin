#include maps\_utility; 
#include common_scripts\utility; 
#include maps\_zombiemode_utility; 

Init()
{
	thread WaitForRoundChange();
    thread WaitForPlayerConnect();
    thread WaitForPowerupSpawned();

    // --- Zombie Round Stats --- //
    // -------------------------- //
    // Kills               AK = Actor Killed
    // Damage              AD = Actor Damaged
    // Downs               PD = Player Downed          
    // Revives             PR = Player Revived        
    // Points              RD = Round Data
    // Round Num           RC = Round Completed
    // PerksConsumed       PC = Perk Consumed
    // PowerupsGrabbed     PG = Powerup Grabbed
    // Game Ended          ME = Match Ended

    // -- Leaderboard (AVG PER GAME?) --
    // Kills / Downs         0.05
    // Headshot Percentage    0.15
    // Melee Kills?           0.225
    // Percentage Alive       0.20
    // Points                 0.175
    // Revives                0.15
    // Highest Round   

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
        
        LogPrint( "GSE;PR;" + BuildPlayerInfoString( self ) + ";" + BuildPlayerInfoString( reviver ) + "\n" );
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
                LogPrint( "GSE;PC;" + BuildPlayerInfoString( self ) + ";" + currentWeapon + "\n" );
                LogPrint( "GSE;SU;" + BuildPlayerInfoString( self ) + ";" + "perks_drank" + ";" + "+1" + "\n" );
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

                LogPrint( "GSE;PG;" + BuildPlayerInfoString( players[i] ) + ";" + powerup + "\n" );
                LogPrint( "GSE;SU;" + BuildPlayerInfoString( players[i] ) + ";" + powerup + "_pickedup" + ";" + "+1" + "\n" );

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
            LogPrint( "GSE;AD;" + victimInfo +  ";" + attackerInfo + ";" + sWeapon + ";" + iDamage + ";" + sMeansOfDeath + ";" + sHitLoc + "\n" );
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
        damage = iDamage;

        if ( IsDefined( eAttacker.maxhealth ) && eAttacker.maxhealth > 0 )
        {
            damage = min( eAttacker.health, iDamage );
        }

        if ( IsPlayer( eInflictor ) )
        {
            attackerInfo = BuildPlayerInfoString( eInflictor );
            damage = min( eInflictor.health, iDamage );
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

    LogPrint( "GSE;PD;" + BuildPlayerInfoString( self ) + "\n" );

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

    LogPrint( "GSE;RC;" + currentRound + "\n" );
}

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
