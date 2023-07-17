/*
* This file contains reusable preprocessor directives meant to be used on
* Plutonium & AlterWare clients that are up to date with the latest version.
* Older versions of Plutonium or other clients do not have support for loading
* or parsing "gsh" files.
*/

/*
* Turn off assertions by removing the following define
* gsc-tool will only emit assertions if developer_script dvar is set to 1
* In short, you should not need to remove this define. Just turn them off
* by using the dvar
*/

#define _INTEGRATION_DEBUG

#ifdef _INTEGRATION_DEBUG

#define _VERIFY( cond, msg ) \
    assertEx( cond, msg )

#else

// This works as an "empty" define here with gsc-tool
#define _VERIFY( cond, msg )

#endif

// This function is meant to be used inside "client commands"
// If the client is not alive it shall return an error message
#define _IS_ALIVE( ent ) \
    _VERIFY( ent, "player entity is not defined" ); \
    if ( !IsAlive( ent ) ) \
    { \
        return ent.name + "^7 is not alive"; \
    }

// This function should be used to verify if a player entity is defined
#define _VERIFY_PLAYER_ENT( ent ) \
    _VERIFY( ent, "player entity is not defined" )
