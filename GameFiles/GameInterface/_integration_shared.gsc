
Init()
{
    level thread Setup();
}

Setup()
{
    level endon( "game_ended" );
    
    level.commonFunctions.changeTeam                        = "ChangeTeam";
    level.commonFunctions.getTeamCounts                     = "GetTeamCounts";
    level.commonFunctions.getMaxClients                     = "GetMaxClients";
    level.commonFunctions.getTeamBased                      = "GetTeamBased";
    level.commonFunctions.getClientTeam                     = "GetClientTeam";
    level.commonFunctions.getClientKillStreak               = "GetClientKillStreak";
    level.commonFunctions.backupRestoreClientKillStreakData = "BackupRestoreClientKillStreakData";
    level.commonFunctions.waitTillAnyTimeout                = "WaitTillAnyTimeout";
    
    level.overrideMethods[level.commonFunctions.changeTeam]                        = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getTeamCounts]                     = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getTeamBased]                      = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getMaxClients]                     = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getClientTeam]                     = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getClientKillStreak]               = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.backupRestoreClientKillStreakData] = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.waitTillAnyTimeout]                = scripts\_integration_base::NotImplementedFunction;
    
    // these can be overridden per game if needed
    level.commonKeys.team1         = "allies"; 
    level.commonKeys.team2         = "axis";
    level.commonKeys.teamSpectator = "spectator";
    
    level.eventTypes.connect     = "connected";
    level.eventTypes.disconnect  = "disconnect";
    level.eventTypes.joinTeam    = "joined_team";
    level.eventTypes.spawned	 = "spawned_player";
    level.eventTypes.gameEnd     = "game_ended";
    
    level.iw4madminIntegrationDefaultPerformance = 200;
    
    if ( GetDvarInt( "sv_iw4madmin_integration_enabled" ) != 1 )
    {
        return;
    }

    if ( GetDvarInt( "sv_iw4madmin_autobalance" ) != 1 )
    {
        return;
    }
    
    level thread OnPlayerConnect();
}

OnPlayerConnect()
{
    level endon( level.eventTypes.gameEnd );

    for ( ;; )
    {
        level waittill( level.eventTypes.connect, player );

        if ( ![[level.overrideMethods[level.commonFunctions.getTeamBased]]]() ) 
        {
            continue;
        }
        
        teamToJoin = player GetTeamToJoin();
        player [[level.overrideMethods[level.commonFunctions.changeTeam]]]( teamToJoin );
        
        player thread OnClientFirstSpawn();
        player thread OnClientJoinedTeam();
        player thread OnClientDisconnect();
        player thread WaitForClientEvents();
    }
}

OnClientDisconnect()
{
    level endon( level.eventTypes.gameEnd );
    self endon( "disconnect_logic_end" );

    for ( ;; )
    {
        self waittill( level.eventTypes.disconnect );
        scripts\_integration_base::LogDebug( "client is disconnecting" );

        OnTeamSizeChanged();
        self notify( "disconnect_logic_end" );
    }
}

OnClientJoinedTeam()
{
    self endon( level.eventTypes.disconnect );

    for( ;; )
    {
        self waittill( level.eventTypes.joinTeam );

        if ( IsDefined( self.wasAutoBalanced ) && self.wasAutoBalanced )
        {
            self.wasAutoBalanced = false;
            continue;
        }
        
        newTeam = self [[level.overrideMethods[level.commonFunctions.getClientTeam]]]();
        scripts\_integration_base::LogDebug( self.name + " switched to " + newTeam );
        
        if ( newTeam != level.commonKeys.team1 && newTeam != level.commonKeys.team2 )
        {
            OnTeamSizeChanged();
            scripts\_integration_base::LogDebug( "not force balancing " + self.name + " because they switched to spec"  );
            continue;
        }
        
        properTeam = self GetTeamToJoin();
        if ( newTeam != properTeam )
        {
            self [[level.overrideMethods[level.commonFunctions.backupRestoreClientKillStreakData]]]( false );
            self [[level.overrideMethods[level.commonFunctions.changeTeam]]]( properTeam );
            wait ( 0.1 );
            self [[level.overrideMethods[level.commonFunctions.backupRestoreClientKillStreakData]]]( true );
        } 		
    }
}

OnClientFirstSpawn()
{
    self endon( level.eventTypes.disconnect );
    timeoutResult = self [[level.overrideMethods[level.commonFunctions.waitTillAnyTimeout]]]( 30, level.eventTypes.spawned );

    if ( timeoutResult != "timeout" )
    {
       return;
    }

    scripts\_integration_base::LogDebug( "moving " + self.name + " to spectator because they did not spawn within expected duration" );
    self [[level.overrideMethods[level.commonFunctions.changeTeam]]]( level.commonKeys.teamSpectator );
}

OnTeamSizeChanged()
{
    if ( level.players.size < 3 )
    {
        scripts\_integration_base::LogDebug( "not enough clients to autobalance" );
        return;
    }

    if ( !IsDefined( GetSmallerTeam( 1 ) ) )
    {
        scripts\_integration_base::LogDebug( "teams are not unbalanced enough to auto balance" );
        return;
    }
    
    toSwap = FindClientToSwap();
    curentTeam = toSwap [[level.overrideMethods[level.commonFunctions.getClientTeam]]]();
    otherTeam = level.commonKeys.team1;

    if ( curentTeam == otherTeam ) 
    {
        otherTeam = level.commonKeys.team2;
    }

    toSwap.wasAutoBalanced = true;
    
    if ( !IsDefined( toSwap.autoBalanceCount ) )
    {
        toSwap.autoBalanceCount = 1;
    }
    else
    {
        toSwap.autoBalanceCount++;
    }

    toSwap [[level.overrideMethods[level.commonFunctions.backupRestoreClientKillStreakData]]]( false );
    scripts\_integration_base::LogDebug( "swapping " + toSwap.name + " from " + curentTeam + " to " + otherTeam );
    toSwap [[level.overrideMethods[level.commonFunctions.changeTeam]]]( otherTeam );
    wait ( 0.1 ); // give the killstreak on team switch clear event time to execute
    toSwap [[level.overrideMethods[level.commonFunctions.backupRestoreClientKillStreakData]]]( true );
}

FindClientToSwap()
{
    smallerTeam = GetSmallerTeam( 1 );
    teamPool = level.commonKeys.team1;

    if ( IsDefined( smallerTeam ) )
    {
        if ( smallerTeam == teamPool )
        {
            teamPool = level.commonKeys.team2;
        }
    }
    else
    {
        teamPerformances = GetTeamPerformances();
        team1Perf = teamPerformances[level.commonKeys.team1];
        team2Perf = teamPerformances[level.commonKeys.team2];
        teamPool = level.commonKeys.team1;

        if ( team2Perf > team1Perf )
        {
            teamPool = level.commonKeys.team2;
        }
    }

    client = GetBestSwapCandidate( teamPool );

    if ( !IsDefined( client ) )
    {
        scripts\_integration_base::LogDebug( "could not find candidate to swap teams" );
    }
    else
    {
        scripts\_integration_base::LogDebug( "best candidate to swap teams is " + client.name );
    }

    return client;
}

GetBestSwapCandidate( team )
{
    candidates = [];
    maxClients = [[level.overrideMethods[level.commonFunctions.getMaxClients]]]();

    for ( i = 0; i < maxClients; i++ )
    {
        candidates[i] = GetClosestPerformanceClientForTeam( team, candidates );
    }

    candidate = undefined;
    
    foundCandidate = false;
    for ( i = 0; i < maxClients; i++ )
    {
        if ( !IsDefined( candidates[i] ) )
        {
            continue;
        }

        candidate = candidates[i];
        candidateKillStreak = candidate [[level.overrideMethods[level.commonFunctions.getClientKillStreak]]]();

        scripts\_integration_base::LogDebug( "candidate killstreak is " + candidateKillStreak );
        
        if ( candidateKillStreak > 3 )
        {
            scripts\_integration_base::LogDebug( "skipping candidate selection for " + candidate.name + " because their kill streak is too high" );
            continue;
        }

        if ( IsDefined( candidate.autoBalanceCount ) && candidate.autoBalanceCount > 2 )
        {
            scripts\_integration_base::LogDebug( "skipping candidate selection for " + candidate.name + " they have been swapped too many times" );
            continue;
        }

        foundCandidate = true;
        break;
    }

    if ( foundCandidate )
    {
        return candidate;
    }

    return undefined;
}

GetClosestPerformanceClientForTeam( sourceTeam, excluded )
{
    if ( !IsDefined( excluded ) )
    {
        excluded = [];
    }

    otherTeam = level.commonKeys.team1;

    if ( sourceTeam == otherTeam ) 
    {
        otherTeam = level.commonKeys.team2;
    }

    teamPerformances = GetTeamPerformances();
    players = level.players;
    choice = undefined;
    closest = 9999999;

    for ( i = 0; i < players.size; i++ )
    {
        isExcluded = false;
        
        for ( j = 0; j < excluded.size; j++ )
        {
            if ( excluded[j] == players[i] )
            {
                isExcluded = true;
                break;
            }
        }

        if ( isExcluded )
        {
            continue;
        }

        if ( players[i] [[level.overrideMethods[level.commonFunctions.getClientTeam]]]() != sourceTeam )
        {
            continue;
        }

        clientPerformance = players[i] GetClientPerformanceOrDefault();
        sourceTeamNewPerformance = teamPerformances[sourceTeam] - clientPerformance;
        otherTeamNewPerformance = teamPerformances[otherTeam] + clientPerformance;
        candidateValue = Abs( sourceTeamNewPerformance - otherTeamNewPerformance );

        scripts\_integration_base::LogDebug( "perf=" + clientPerformance + " candidateValue=" + candidateValue + " src=" + sourceTeamNewPerformance + " dst=" + otherTeamNewPerformance );

        if ( !IsDefined( choice ) )
        {
            choice = players[i];
            closest = candidateValue;
        } 

        else if ( candidateValue < closest )
        {
            scripts\_integration_base::LogDebug( candidateValue + " is the new best value ");
            choice = players[i];
            closest = candidateValue;
        }
    }

    scripts\_integration_base::LogDebug( choice.name + " is the best candidate to swap" + " with closest=" + closest );
    return choice;
}

GetTeamToJoin()
{
    smallerTeam = GetSmallerTeam( 1 );

    if ( IsDefined( smallerTeam ) )
    {
        return smallerTeam;
    }
    
    teamPerformances = GetTeamPerformances( self );
    
    if ( teamPerformances[level.commonKeys.team1] < teamPerformances[level.commonKeys.team2] )
    {
        scripts\_integration_base::LogDebug( "Team1 performance is lower, so selecting Team1" );
        return level.commonKeys.team1;
    }
    
    else
    {
        scripts\_integration_base::LogDebug( "Team2 performance is lower, so selecting Team2" );
        return level.commonKeys.team2;
    }
}

GetSmallerTeam( minDiff )
{
    teamCounts = [[level.overrideMethods[level.commonFunctions.getTeamCounts]]]();
    team1Count = teamCounts[level.commonKeys.team1];
    team2Count = teamCounts[level.commonKeys.team2];
    maxClients = [[level.overrideMethods[level.commonFunctions.getMaxClients]]]();

    if ( team1Count == team2Count )
    {
        return undefined;
    }
    
    if ( team2Count == maxClients / 2 )
    {
        scripts\_integration_base::LogDebug( "Team2 is full, so selecting Team1" );
        return level.commonKeys.team1;
    }
    
    if ( team1Count == maxClients / 2 )
    {
        scripts\_integration_base::LogDebug( "Team1 is full, so selecting Team2" );
        return level.commonKeys.team2;
    }
    
    sizeDiscrepancy = Abs( team1Count - team2Count );
    
    if ( sizeDiscrepancy > minDiff ) 
    {
        scripts\_integration_base::LogDebug( "Team size differs by more than 1" );
        
        if ( team1Count < team2Count )
        {
            scripts\_integration_base::LogDebug( "Team1 is smaller, so selecting Team1" );
            return level.commonKeys.team1;
        }
        
        else
        {
            scripts\_integration_base::LogDebug( "Team2 is smaller, so selecting Team2" );
            return level.commonKeys.team2;
        }
    }

    return undefined;
}

GetTeamPerformances( ignoredClient )
{
    players = level.players;
    
    team1 = 0;
    team2 = 0;
    
    for ( i = 0; i < players.size; i++ )
    {
        if ( IsDefined( ignoredClient ) && players[i] == ignoredClient )
        {
            continue;
        }
        
        performance = players[i] GetClientPerformanceOrDefault();
        clientTeam = players[i] [[level.overrideMethods[level.commonFunctions.getClientTeam]]]();
        
        if ( clientTeam == level.commonKeys.team1 )
        {
            team1 = team1 + performance;
        }
        else
        {
            team2 = team2 + performance;
        }
    }
    
    result = [];
    result[level.commonKeys.team1] = team1;
    result[level.commonKeys.team2] = team2;
    return result;
}

GetClientPerformanceOrDefault()
{
    clientData = self.pers[level.clientDataKey];
    performance = level.iw4madminIntegrationDefaultPerformance;
        
    if ( IsDefined( clientData ) && IsDefined( clientData.performance ) ) 
    {
        performance = int( clientData.performance );
    }

    return performance;
}

WaitForClientEvents()
{
    self endon( level.eventTypes.disconnect );
    
    for ( ;; )
    {
        self waittill( level.eventTypes.localClientEvent, event );

        if ( event.type == level.eventTypes.clientDataReceived )
        {
            clientData = self.pers[level.clientDataKey];
        }
    }
}
