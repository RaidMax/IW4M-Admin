Init()
{
    thread Setup();
}

Setup()
{
    wait ( 0.05 );
    level endon( "game_ended" );
    
    level waittill( level.notifyTypes.integrationBootstrapInitialized );
    
    level.commonFunctions.changeTeam                        = "ChangeTeam";
    level.commonFunctions.getTeamCounts                     = "GetTeamCounts";
    level.commonFunctions.getMaxClients                     = "GetMaxClients";
    level.commonFunctions.getTeamBased                      = "GetTeamBased";
    level.commonFunctions.getClientTeam                     = "GetClientTeam";
    level.commonFunctions.getClientKillStreak               = "GetClientKillStreak";
    level.commonFunctions.backupRestoreClientKillStreakData = "BackupRestoreClientKillStreakData";
    level.commonFunctions.getTotalShotsFired                = "GetTotalShotsFired"; 
    level.commonFunctions.waitTillAnyTimeout                = "WaitTillAnyTimeout";
    level.commonFunctions.isBot                             = "IsBot";
    level.commonFunctions.getXuid                           = "GetXuid";
    
    level.overrideMethods[level.commonFunctions.changeTeam]                        = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getTeamCounts]                     = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getTeamBased]                      = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getMaxClients]                     = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getClientTeam]                     = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getClientKillStreak]               = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.backupRestoreClientKillStreakData] = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.waitTillAnyTimeout]                = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.getXuid]                           = scripts\_integration_base::NotImplementedFunction;
    level.overrideMethods[level.commonFunctions.isBot]                             = scripts\_integration_base::NotImplementedFunction;
    
    // these can be overridden per game if needed
    level.commonKeys.team1         = "allies"; 
    level.commonKeys.team2         = "axis";
    level.commonKeys.teamSpectator = "spectator";
    level.commonKeys.autoBalance   = "sv_iw4madmin_autobalance";
    
    level.eventTypes.connect     = "connected";
    level.eventTypes.disconnect  = "disconnect";
    level.eventTypes.joinTeam    = "joined_team";
    level.eventTypes.joinSpec    = "joined_spectators";
    level.eventTypes.spawned	 = "spawned_player";
    level.eventTypes.gameEnd     = "game_ended";

    level.eventTypes.urlRequested             = "UrlRequested";
    level.eventTypes.urlRequestCompleted      = "UrlRequestCompleted";
    level.eventTypes.registerCommandRequested = "RegisterCommandRequested";
    level.eventTypes.getCommandsRequested     = "GetCommandsRequested";
    level.eventTypes.getBusModeRequested      = "GetBusModeRequested";

    level.eventCallbacks[level.eventTypes.urlRequestCompleted]  = ::OnUrlRequestCompletedCallback;
    level.eventCallbacks[level.eventTypes.getCommandsRequested] = ::OnCommandsRequestedCallback;
    level.eventCallbacks[level.eventTypes.getBusModeRequested]  = ::OnBusModeRequestedCallback;
    
    level.iw4madminIntegrationDefaultPerformance = 200;
    level.notifyEntities = [];
    level.customCommands = [];
    
    level notify( level.notifyTypes.sharedFunctionsInitialized );
    level waittill( level.notifyTypes.gameFunctionsInitialized );

    scripts\_integration_base::_SetDvarIfUninitialized( level.commonKeys.autoBalance, 0 ); 

    if ( GetDvarInt( level.commonKeys.enabled ) != 1 )
    {
        return;
    }
    
    thread OnPlayerConnect();
}

_IsBot( player )
{
    return [[level.overrideMethods[level.commonFunctions.isBot]]]( player );
}

OnPlayerConnect()
{
    level endon( level.eventTypes.gameEnd );

    for ( ;; )
    {
        level waittill( level.eventTypes.connect, player );
        
        if ( _IsBot( player ) ) 
        {
            // we don't want to track bots
            continue;
        }

        if ( !IsDefined( player.pers[level.clientDataKey] ) )
        {
            player.pers[level.clientDataKey] = spawnstruct();
        }
        
        player thread OnPlayerSpawned();
        player thread OnPlayerJoinedTeam();
        player thread OnPlayerJoinedSpectators();
        player thread PlayerTrackingOnInterval();

        if ( GetDvarInt( level.commonKeys.autoBalance ) != 1 || !IsDefined( [[level.overrideMethods[level.commonFunctions.getTeamBased]]]() ) ) 
        {
            continue;
        }
        
        if ( ![[level.overrideMethods[level.commonFunctions.getTeamBased]]]() ) 
        {
            continue;
        }
        
        teamToJoin = player GetTeamToJoin();
        player [[level.overrideMethods[level.commonFunctions.changeTeam]]]( teamToJoin );
        
        player thread OnPlayerFirstSpawn();
        player thread OnPlayerDisconnect();
    }
}

PlayerSpawnEvents() 
{
    self endon( level.eventTypes.disconnect );

    clientData = self.pers[level.clientDataKey];
    
    // this gives IW4MAdmin some time to register the player before making the request;
    // although probably not necessary some users might have a slow database or poll rate
    wait ( 2 );

    if ( IsDefined( clientData.state ) && clientData.state == "complete" ) 
    {
        return;
    }
    
    self scripts\_integration_base::RequestClientBasicData();

    self waittill( level.eventTypes.clientDataReceived, clientEvent );

    if ( clientData.permissionLevel == "User" || clientData.permissionLevel == "Flagged" ) 
    {
        return;
    } 

    self IPrintLnBold( "Welcome, your level is ^5" + clientData.permissionLevel );
    wait( 2.0 );
    self IPrintLnBold( "You were last seen ^5" + clientData.lastConnection + " ago" );
}


PlayerTrackingOnInterval() 
{
    self endon( level.eventTypes.disconnect );

    for ( ;; )
    {
        wait ( 120 );
        if ( IsAlive( self ) )
        {
            self SaveTrackingMetrics();
        }
    }
}

SaveTrackingMetrics()
{
    if ( !IsDefined( self.persistentClientId ) )
    {
        return;
    }

    scripts\_integration_base::LogDebug( "Saving tracking metrics for " + self.persistentClientId );
    
    if ( !IsDefined( self.lastShotCount ) )
    {
        self.lastShotCount = 0;
    }

    currentShotCount = self [[level.overrideMethods["GetTotalShotsFired"]]]();
    change = currentShotCount - self.lastShotCount;
    self.lastShotCount = currentShotCount;

    scripts\_integration_base::LogDebug( "Total Shots Fired increased by " + change );

    if ( !IsDefined( change ) )
    {
        change = 0;
    }
    
    if ( change == 0 )
    {
        return;
    }

    scripts\_integration_base::IncrementClientMeta( "TotalShotsFired", change, self.persistentClientId );
}

OnBusModeRequestedCallback( event )
{
    data = [];
    data["mode"] = GetDvar( level.commonKeys.busMode );
    data["directory"] = GetDvar( level.commonKeys.busDir );
    data["inLocation"] = level.eventBus.inLocation;
    data["outLocation"] = level.eventBus.outLocation;

    scripts\_integration_base::LogDebug( "Bus mode requested" );

    busModeRequest = scripts\_integration_base::BuildEventRequest( false, level.eventTypes.getBusModeRequested, "", undefined, data );
    scripts\_integration_base::QueueEvent( busModeRequest, level.eventTypes.getBusModeRequested, undefined );

    scripts\_integration_base::LogDebug( "Bus mode updated" );

    if ( GetDvar( level.commonKeys.busMode ) == "file" && GetDvar( level.commonKeys.busDir ) != "" )
    {
        level.busMethods[level.commonFunctions.getInboundData]  = level.overrideMethods[level.commonFunctions.getInboundData]; 
        level.busMethods[level.commonFunctions.getOutboundData] = level.overrideMethods[level.commonFunctions.getOutboundData];
        level.busMethods[level.commonFunctions.setInboundData]  = level.overrideMethods[level.commonFunctions.setInboundData]; 
        level.busMethods[level.commonFunctions.setOutboundData] = level.overrideMethods[level.commonFunctions.setOutboundData];
    }
}

// #region register script command

OnCommandsRequestedCallback( event )
{
    scripts\_integration_base::LogDebug( "Get commands requested" );
    thread SendCommands( event.data["name"] );
}

SendCommands( commandName )
{
    level endon( level.eventTypes.gameEnd );

    for ( i = 0; i < level.customCommands.size; i++ )
    {
        data = level.customCommands[i];

        if ( IsDefined( commandName ) && commandName != data["name"] )
        {
            continue;        
        }

        scripts\_integration_base::LogDebug( "Sending custom command " + ( i + 1 ) + "/" + level.customCommands.size + ": " + data["name"] );
        commandRegisterRequest = scripts\_integration_base::BuildEventRequest( false, level.eventTypes.registerCommandRequested, "", undefined, data );
        // not threading here as there might be a lot of commands to register
        scripts\_integration_base::QueueEvent( commandRegisterRequest, level.eventTypes.registerCommandRequested, undefined );
    }
}

RegisterScriptCommandObject( command )
{
    RegisterScriptCommand( command.eventKey, command.name, command.alias, command.description, command.minPermission, command.supportedGames, command.requiresTarget, command.handler );
}

RegisterScriptCommand( eventKey, name, alias, description, minPermission, supportedGames, requiresTarget, handler )
{
    if ( !IsDefined( eventKey ) )
    {
        scripts\_integration_base::LogError( "eventKey must be provided for script command" );
        return;
    }

    data = [];

    data["eventKey"] = eventKey;

    if ( IsDefined( name ) )
    {
        data["name"] = name;
    }
    else
    {
        scripts\_integration_base::LogError( "name must be provided for script command" );
        return;
    }

    if ( IsDefined( alias ) )
    {
        data["alias"] = alias;
    }

    if ( IsDefined( description ) )
    {
        data["description"] = description;
    }

    if ( IsDefined( minPermission ) )
    {
        data["minPermission"] = minPermission;
    }
    
    if ( IsDefined( supportedGames ) )
    {
        data["supportedGames"] = supportedGames;
    }

    data["requiresTarget"] = false;

    if ( IsDefined( requiresTarget ) )
    {
        data["requiresTarget"] = requiresTarget;
    }

    if ( IsDefined( handler ) )
    {
        level.clientCommandCallbacks[eventKey + "Execute"] = handler;
        level.clientCommandRusAsTarget[eventKey + "Execute"] = data["requiresTarget"];
    }
    else
    {
        scripts\_integration_base::LogWarning( "handler not defined for script command " + name );
    }

    level.customCommands[level.customCommands.size] = data;
}

// #end region

// #region web requests

RequestUrlObject( request )
{
    return RequestUrl( request.url, request.method, request.body, request.headers, request );
}

RequestUrl( url, method, body, headers, webNotify )
{
    if ( !IsDefined( webNotify ) )
    {
        webNotify = SpawnStruct();
        webNotify.url = url;
        webNotify.method = method;
        webNotify.body = body;
        webNotify.headers = headers;
    }

    webNotify.index = GetNextNotifyEntity();

    scripts\_integration_base::LogDebug( "next notify index is " + webNotify.index );
    level.notifyEntities[webNotify.index] = webNotify;

    data = [];
    data["url"] = webNotify.url;
    data["entity"] = webNotify.index;

    if ( IsDefined( method ) )
    {
        data["method"] = method;
    }

    if ( IsDefined( body ) )
    {
        data["body"] = body;
    }

    if ( IsDefined( headers ) )
    {
        headerString = "";

        keys = GetArrayKeys( headers );
        for ( i = 0; i < keys.size; i++ )
        {
            headerString = headerString + keys[i] + ":" + headers[keys[i]] + ",";
        }

        data["headers"] = headerString;
    }

    webNotifyEvent = scripts\_integration_base::BuildEventRequest( true, level.eventTypes.urlRequested, "", webNotify.index, data );
    thread scripts\_integration_base::QueueEvent( webNotifyEvent, level.eventTypes.urlRequested, webNotify );
    webNotify thread WaitForUrlRequestComplete();

    return webNotify;
}

WaitForUrlRequestComplete()
{
    level endon( level.eventTypes.gameEnd );

    timeoutResult = self [[level.overrideMethods[level.commonFunctions.waitTillAnyTimeout]]]( 30, level.eventTypes.urlRequestCompleted );

    if ( timeoutResult == level.eventBus.timeoutKey )
    {
        scripts\_integration_base::LogWarning( "Request to " + self.url  + " timed out" );
        self notify ( level.eventTypes.urlRequestCompleted, "error" );
    }

    scripts\_integration_base::LogDebug( "Request to " + self.url  + " completed" );

    level.notifyEntities[self.index] = undefined;
}

OnUrlRequestCompletedCallback( event )
{
    if ( !IsDefined( event ) || !IsDefined( event.data ) )
    {
        scripts\_integration_base::LogWarning( "Incomplete data for url request callback. [1]" );
        return;
    }

    notifyEnt = event.data["entity"];
    response = event.data["response"];

    if ( !IsDefined( notifyEnt ) || !IsDefined( response ) )
    {
        scripts\_integration_base::LogWarning( "Incomplete data for url request callback. [2] " + scripts\_integration_base::CoerceUndefined( notifyEnt ) + " , " +  scripts\_integration_base::CoerceUndefined( response ) );
        return;
    }

    webNotify = level.notifyEntities[int( notifyEnt )];
    
    if ( !IsDefined( webNotify.response ) )
    {
        webNotify.response = response;
    }
    else
    {
        webNotify.response = webNotify.response + response;
    }

    if ( int( event.data["remaining"] ) != 0 ) 
    {
        scripts\_integration_base::LogDebug( "Additional data available for url request " + notifyEnt + " (" + event.data["remaining"] + " chunks remaining)" );
        return;
    }

    scripts\_integration_base::LogDebug( "Notifying " + notifyEnt + " that url request completed"  );
    webNotify notify( level.eventTypes.urlRequestCompleted, webNotify.response );
}

GetNextNotifyEntity()
{
    max = level.notifyEntities.size + 1;

    for ( i = 0; i < max; i++ )
    {
        if ( !IsDefined( level.notifyEntities[i] ) )
        {
            return i;
        }
    }

    return max;
}

// #end region

// #region team balance

OnPlayerDisconnect()
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

OnPlayerJoinedTeam()
{
    self endon( level.eventTypes.disconnect );

    for( ;; )
    {
        self waittill( level.eventTypes.joinTeam );

        wait( 0.25 ); 
        LogPrint( GenerateJoinTeamString( false ) );

        if ( GetDvarInt( level.commonKeys.autoBalance ) != 1 )
        {
            continue;
        }

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
            scripts\_integration_base::LogDebug( "not force balancing " + self.name + " because they switched to spec" );
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

OnPlayerSpawned()
{
    self endon( level.eventTypes.disconnect );

    for ( ;; )
    {
        self waittill( level.eventTypes.spawned );
        self thread PlayerSpawnEvents();
    }
}

OnPlayerJoinedSpectators()
{
    self endon( level.eventTypes.disconnect );

    for( ;; )
    {
        self waittill( level.eventTypes.joinSpec );
        LogPrint( GenerateJoinTeamString( true ) );
    }
}

OnPlayerFirstSpawn()
{
    self endon( level.eventTypes.disconnect );
    timeoutResult = self [[level.overrideMethods[level.commonFunctions.waitTillAnyTimeout]]]( 30, level.eventTypes.spawned );

    if ( timeoutResult != level.eventBus.timeoutKey )
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
            scripts\_integration_base::LogDebug( candidateValue + " is the new best value " );
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

GenerateJoinTeamString( isSpectator ) 
{
    team = self.team;

    if ( IsDefined( self.joining_team ) )
    {
        team = self.joining_team;
    }
    else
    {
        if ( isSpectator || !IsDefined( team ) ) 
        {
            team = "spectator";
        }
    }

    guid = self [[level.overrideMethods[level.commonFunctions.getXuid]]]();

    if ( guid == "0" )
    {
        guid = self.guid;
    }

    if ( !IsDefined( guid ) || guid == "0" )
    {
        guid = "undefined";
    }

    return "JT;" + guid + ";" + self getEntityNumber() + ";" + team + ";" + self.name + "\n";
}

// #end region
