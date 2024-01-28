Init()
{
    thread Setup();
}

Setup()
{
    level endon( "game_ended" );
    waittillframeend;

    level waittill( level.notifyTypes.sharedFunctionsInitialized );

    scripts\_integration_base::RegisterLogger( ::Log2Console );

    level.overrideMethods[level.commonFunctions.getTotalShotsFired]         = ::GetTotalShotsFired;
    level.overrideMethods[level.commonFunctions.setDvar]                    = ::SetDvarIfUninitializedWrapper;
    level.overrideMethods[level.commonFunctions.waittillNotifyOrTimeout]    = ::WaitillNotifyOrTimeoutWrapper;
    level.overrideMethods[level.commonFunctions.isBot]                      = ::IsBotWrapper;
    level.overrideMethods[level.commonFunctions.getXuid]                    = ::GetXuidWrapper;
    level.overrideMethods[level.commonFunctions.waitTillAnyTimeout]      = ::WaitTillAnyTimeout;

    level notify( level.notifyTypes.gameFunctionsInitialized );
}

GetTotalShotsFired()
{
    return maps\mp\_utility::getPlayerStat( "mostshotsfired" );
}

SetDvarIfUninitializedWrapper( dvar, value )
{
    SetDvarIfUninitialized( dvar, value );
}

WaitillNotifyOrTimeoutWrapper( _notify, timeout )
{
    common_scripts\utility::waittill_notify_or_timeout( _notify, timeout );
}

Log2Console( logLevel, message ) 
{
    Print( "[" + logLevel + "] " + message + "\n" );
}

IsBotWrapper( client )
{
    return IsBot( client ); 
}

GetXuidWrapper()
{
    return self GetXUID();
}

WaitTillAnyTimeout( timeOut, string1, string2, string3, string4, string5 )
{
    return common_scripts\utility::waittill_any_timeout( timeOut, string1, string2, string3, string4, string5 );
}