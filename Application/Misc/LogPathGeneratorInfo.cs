using System.Runtime.InteropServices;

namespace IW4MAdmin.Application.Misc
{
    /// <summary>
    /// dto class for handling log path generation
    /// </summary>
    public class LogPathGeneratorInfo
    {
        /// <summary>
        /// directory under the paths where data comes from by default
        /// <remarks>fs_basegame</remarks>
        /// </summary>
        public string BaseGameDirectory { get; set; } = "";

        /// <summary>
        /// base game root path
        /// <remarks>fs_basepath</remarks>
        /// </summary>
        public string BasePathDirectory { get; set; } = "";

        /// <summary>
        /// overide game directory
        /// <remarks>plugin driven</remarks>
        /// </summary>
        public string GameDirectory { get; set; } = "";

        /// <summary>
        /// game director
        /// <remarks>fs_game</remarks>
        /// </summary>
        public string ModDirectory { get; set; } = "";

        /// <summary>
        /// log file name
        /// <remarks>g_log</remarks>
        /// </summary>
        public string LogFile { get; set; } = "";

        /// <summary>
        /// indicates if running on windows
        /// </summary>
        public bool IsWindows { get; set; } = true;
        
        /// <summary>
        /// indicates that the game does not log to the mods folder (when mod is loaded),
        /// but rather always to the fs_basegame directory
        /// </summary>
        public bool IsOneLog { get; set; }
    }
}
