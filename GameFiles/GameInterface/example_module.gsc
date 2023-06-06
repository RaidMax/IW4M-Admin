Init()
{
    // this gives the game interface time to setup
    waittillframeend;
    thread ModuleSetup();
}

ModuleSetup()
{
    // waiting until the game specific functions are ready
    level waittill( level.notifyTypes.gameFunctionsInitialized );

    RegisterCustomCommands();
}

RegisterCustomCommands()
{
    command = SpawnStruct();
    
    // unique key for each command (how iw4madmin identifies the command)
    command.eventKey = "PrintLineCommand";

    // name of the command (cannot conflict with existing command names)
    command.name = "println";
    
    // short version of the command (cannot conflcit with existing command aliases)
    command.alias = "pl";

    // description of what the command does
    command.description = "prints line to game";
    
    // minimum permision required to execute
    // valid values: User, Trusted, Moderator, Administrator, SeniorAdmin, Owner
    command.minPermission = "Trusted";

    // games the command is supported on
    // separate with comma or don't define for all
    // valid values: IW3, IW4, IW5, IW6, T4, T5, T6, T7, SHG1, CSGO, H1
    command.supportedGames = "IW4,IW5,T5,T6";

    // indicates if a target player must be provided to execvute on
    command.requiresTarget = false;

    // code to run when the command is executed
    command.handler = ::PrintLnCommandCallback;
    
    // register the command with integration to be send to iw4madmin
    scripts\_integration_shared::RegisterScriptCommandObject( command );

    // you can also register via parameters
    scripts\_integration_shared::RegisterScriptCommand( "AffirmationCommand", "affirm", "af", "provide affirmations", "User", undefined, false, ::AffirmationCommandCallback );
}

PrintLnCommandCallback( event )
{
    if ( IsDefined( event.data["args"] ) )
    {
        IPrintLnBold( event.data["args"] );
        return;
    }

    scripts\_integration_base::LogDebug( "No data was provided for PrintLnCallback" );
}

AffirmationCommandCallback( event, _ )
{
    level endon( level.eventTypes.gameEnd );

    request         = SpawnStruct();
    request.url     = "https://www.affirmations.dev";
    request.method  = "GET";

    // If making a post request you can also provide more data
    // request.body = "Body of the post message";
    // request.headers = [];
    // request.headers["Authorization"] = "api-key";

    scripts\_integration_shared::RequestUrlObject( request );
    request waittill( level.eventTypes.urlRequestCompleted, response );

    // horrible json parsing.. but it's just an example
    parsedResponse = strtok( response, "\"" );

    if ( IsPlayer( self ) )
    {
        self IPrintLnBold ( "^5" + parsedResponse[parsedResponse.size - 2] );
    }
}
