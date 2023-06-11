/*********************************************************************************
 * DISCLAIMER:                                                                   *
 *                                                                               *
 * This script is optional and not required for                                  *
 * standard functionality. To use this script, a third-party                     *
 * plugin named "t6-gsc-utils" must be installed on the                          *
 * game server in the "*\storage\t6\plugins" folder                              *
 *                                                                               *
 * The "t6-gsc-utils" plugin can be obtained from the GitHub                     *
 * repository at:                                                                *
 * https://github.com/fedddddd/t6-gsc-utils                                      *
 *                                                                               *
 * Please make sure to install the plugin before running this                    *
 * script.                                                                       *
 *********************************************************************************/

/*********************************************************************************
 * FUNCTIONALITY:                                                                *
 *                                                                               *
 * This script extends the game interface to support the "file"                  *
 * bus mode for Plutonium T6, which allows the game server and IW4M-Admin        *
 * to communicate via files, rather than over rcon using                         *
 * dvars.                                                                        *
 *                                                                               *
 * By enabling the "file" bus mode, IW4M-Admin can send                          *
 * commands and receive responses from the game server by                        *
 * reading and writing to specific files. This provides a                        *
 * flexible and efficient communication channel.                                 *
 *                                                                               *
 * Make sure to configure the server to use the "file" bus                       *
 * mode and set the appropriate file path to                                     *
 * establish the communication between IW4M-Admin and the                        *
 * game server.                                                                  *
 *                                                                               *
 * The wiki page for the setup of the game interface, and the bus mode           *
 * can be found on GitHub at:                                                    *
 * https://github.com/RaidMax/IW4M-Admin/wiki/GameInterface#configuring-bus-mode *
 *********************************************************************************/

Init()
{
	thread Setup();
}

Setup()
{
	level waittill( level.notifyTypes.sharedFunctionsInitialized );
	level.overrideMethods[level.commonFunctions.getInboundData]  = ::GetInboundData;
	level.overrideMethods[level.commonFunctions.getOutboundData] = ::GetOutboundData;
	level.overrideMethods[level.commonFunctions.setInboundData]  = ::SetInboundData;
	level.overrideMethods[level.commonFunctions.setOutboundData] = ::SetOutboundData;
	scripts\_integration_base::_SetDvarIfUninitialized( level.commonKeys.busdir, GetDvar( "fs_homepath" ) );
}

GetInboundData( location )
{
	return readFile( location );
}

GetOutboundData( location )
{
	return readFile( location );
}

SetInboundData( location, data )
{
	writeFile( location, data );
}

SetOutboundData( location, data )
{
	writeFile( location, data );
}
