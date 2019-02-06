using System.Collections.Generic;
using System.Threading.Tasks;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Objects;
using SharedLibraryCore.RCon;
using static SharedLibraryCore.Server;

namespace SharedLibraryCore.Interfaces
{
    public interface IRConParser
    {
        /// <summary>
        /// retrieves the value of a given DVAR
        /// </summary>
        /// <typeparam name="T">type of DVAR expected (string, int, float etc...)</typeparam>
        /// <param name="connection">RCon connection to retrieve with</param>
        /// <param name="dvarName">name of DVAR</param>
        /// <returns></returns>
        Task<Dvar<T>> GetDvarAsync<T>(Connection connection, string dvarName);

        /// <summary>
        /// set value of DVAR by name
        /// </summary>
        /// <param name="connection">RCon connection to use</param>
        /// <param name="dvarName">name of DVAR to set</param>
        /// <param name="dvarValue">value to set DVAR to</param>
        /// <returns></returns>
        Task<bool> SetDvarAsync(Connection connection, string dvarName, object dvarValue);

        /// <summary>
        /// executes a console command on the server
        /// </summary>
        /// <param name="connection">RCon connection to use</param>
        /// <param name="command">console command to execute</param>
        /// <returns></returns>
        Task<string[]> ExecuteCommandAsync(Connection connection, string command);

        /// <summary>
        /// get the list of connected clients from status response
        /// </summary>
        /// <param name="connection">RCon connection to use</param>
        /// <returns></returns>
        Task<List<EFClient>> GetStatusAsync(Connection connection);

        /// <summary>
        /// stores the RCon configuration
        /// </summary>
        IRConParserConfiguration Configuration { get; set; }

        /// <summary>
        /// stores the game/client specific version (usually the value of the "version" DVAR)
        /// </summary>
        string Version { get; set; }

        /// <summary>
        /// specifies the game name (usually the internal studio iteration ie: IW4, T5 etc...)
        /// </summary>
        Game GameName { get; set; }
        
        /// <summary>
        /// indicates if the game supports generating a log path from DVAR retrieval
        /// of fs_game, fs_basepath, g_log
        /// </summary>
        bool CanGenerateLogPath { get; set; }
    }
}
