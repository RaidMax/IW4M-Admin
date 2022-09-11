init()
{
    level thread InitializeGameMethods();
}


InitializeGameMethods()
{
    level waittill("InitializeGameMethods");

    level.overrideMethods["god"] = ::_god;
    level.overrideMethods["noclip"] = level.overrideMethods["UnsupportedFunc"];
    level.overrideMethods["waittill_notify_or_timeout"] = common_scripts\utility::waittill_notify_or_timeout;
    level.overrideMethods["GetTotalShotsFired"] = ::GetTotalShotsFired;
    level.overrideMethods["SetDvarIfUninitialized"] = ::SetDvarIfUninitialized;
    level.overrideMethods["GetPlayerData"] = ::GetPlayerData;
    level.overrideMethods["SetPlayerData"] = ::SetPlayerData;
    
    if ( isDefined( ::God ) )
    {
        level.overrideMethods["god"] = ::God;
    }
    
    if ( isDefined( ::NoClip ) )
    {
        level.overrideMethods["noclip"] = ::NoClip;
    }

    if ( level.eventBus.gamename == "IW5" ) 
    { //PlutoIW5 only allows Godmode and NoClip if cheats are on..
        level.overrideMethods["god"] = ::IW5_God;
        level.overrideMethods["noclip"] = ::IW5_NoClip;
    }

    level notify("InitializeGameMethodsDone");
}

GetTotalShotsFired()
{
    return maps\mp\_utility::getPlayerStat( "mostshotsfired" );
}

//////////////////////////////////
// Function Overrides
//////////////////////////////////

_god( isEnabled ) 
{
    if ( isEnabled == true ) 
    {
        if ( !IsDefined( self.savedHealth ) || self.health < 1000  )
        {
            self.savedHealth = self.health;
            self.savedMaxHealth = self.maxhealth;
        }
        
        self.maxhealth = 99999;
        self.health = 99999;
    }
    
    else 
    {
        if ( !IsDefined( self.savedHealth ) || !IsDefined( self.savedMaxHealth ) )
        {
            return;
        }
        
        self.health = self.savedHealth;
        self.maxhealth = self.savedMaxHealth;
    }
}


IW5_God()
{
    SetDvar( "sv_cheats", 1 );
    self God();
    SetDvar( "sv_cheats", 0 );
}

IW5_NoClip()
{
    SetDvar( "sv_cheats", 1 );
    self NoClip();
    SetDvar( "sv_cheats", 0 );
}