#include maps\mp\_utility;

init()
{

    level thread InitializeGameMethods();
}


InitializeGameMethods()
{
    level waittill("InitializeGameMethods");

    level.overrideMethods["god"] = ::_god;
    level.overrideMethods["noclip"] = level.overrideMethods["UnsupportedFunc"];
    level.overrideMethods["waittill_notify_or_timeout"] = ::waittill_notify_or_timeout;
    level.overrideMethods["GetTotalShotsFired"] = ::GetTotalShotsFired;
    level.overrideMethods["SetDvarIfUninitialized"] = ::SetDvarIfUninitialized;
    level.overrideMethods["GetPlayerData"] = ::_getdstat;
    level.overrideMethods["SetPlayerData"] = ::_setdstat;


    level notify("InitializeGameMethodsDone");
}

//////////////////////////////////
// Function Overrides
//////////////////////////////////


//TODO: Figure out why we can't manipulate stats. The base game calls these functions.
_getdstat( a, b, c, d, e, f, g, h, i, j, k, l, m, n, o, p, q, r, s, t, u, v, w, x, y, z )
{
    //getdstat( a, b, c, d, e, f, g, h, i, j, k, l, m, n, o, p, q, r, s, t, u, v, w, x, y, z );
    return 0;
}

_setdstat( a, b, c, d, e, f, g, h, i, j, k, l, m, n, o, p, q, r, s, t, u, v, w, x, y, z )
{
    //setdstat( a, b, c, d, e, f, g, h, i, j, k, l, m, n, o, p, q, r, s, t, u, v, w, x, y, z );
}

GetTotalShotsFired()
{
    return maps\mp\gametypes\_persistence::statGet( "total_shots" );
}

SetDvarIfUninitialized(dvar, value)
{
    maps\mp\_utility::set_dvar_if_unset(dvar, value);
}

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

waittill_notify_or_timeout( msg, timer )
{
	self endon( msg );
	wait( timer );
}