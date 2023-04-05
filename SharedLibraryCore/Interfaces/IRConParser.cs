using System;
using System.Threading;
using System.Threading.Tasks;
using static SharedLibraryCore.Server;

namespace SharedLibraryCore.Interfaces
{
    public interface IRConParser
    {
        /// <summary>
        ///     stores the RCon configuration
        /// </summary>
        IRConParserConfiguration Configuration { get; set; }

        /// <summary>
        ///     stores the game/client specific version (usually the value of the "version" DVAR)
        /// </summary>
        string Version { get; }

        /// <summary>
        ///     specifies the game name (usually the internal studio iteration ie: IW4, T5 etc...)
        /// </summary>
        Game GameName { get; }

        /// <summary>
        ///     indicates if the game supports generating a log path from DVAR retrieval
        ///     of fs_game, fs_basepath, g_log
        /// </summary>
        bool CanGenerateLogPath { get; }

        /// <summary>
        ///     specifies the name of the parser
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     specifies the type of rcon engine
        ///     eg: COD, Source
        /// </summary>
        string RConEngine { get; }

        /// <summary>
        ///     indicates that the game does not log to the mods folder (when mod is loaded),
        ///     but rather always to the fs_basegame directory
        /// </summary>
        bool IsOneLog { get; }

        /// <summary>
        ///     retrieves the value of a given DVAR
        /// </summary>
        /// <typeparam name="T">type of DVAR expected (string, int, float etc...)</typeparam>
        /// <param name="connection">RCon connection to retrieve with</param>
        /// <param name="dvarName">name of DVAR</param>
        /// <param name="fallbackValue">default value to return if dvar retrieval fails</param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<Dvar<T>> GetDvarAsync<T>(IRConConnection connection, string dvarName, T fallbackValue = default, CancellationToken token = default);

        /// <summary>
        ///     set value of DVAR by name
        /// </summary>
        /// <param name="connection">RCon connection to use</param>
        /// <param name="dvarName">name of DVAR to set</param>
        /// <param name="dvarValue">value to set DVAR to</param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<bool> SetDvarAsync(IRConConnection connection, string dvarName, object dvarValue, CancellationToken token = default);
  
        /// <summary>
        ///     executes a console command on the server
        /// </summary>
        /// <param name="connection">RCon connection to use</param>
        /// <param name="command">console command to execute</param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<string[]> ExecuteCommandAsync(IRConConnection connection, string command, CancellationToken token = default);

        /// <summary>
        ///     get the list of connected clients from status response
        /// </summary>
        /// <param name="connection">RCon connection to use</param>
        /// <param name="token"></param>
        /// <returns>
        ///     <see cref="IStatusResponse" />
        /// </returns>
        Task<IStatusResponse> GetStatusAsync(IRConConnection connection, CancellationToken token = default);

        /// <summary>
        ///     retrieves the value of given dvar key if it exists in the override dict
        ///     otherwise returns original
        /// </summary>
        /// <param name="dvarName">name of dvar key</param>
        /// <returns></returns>
        string GetOverrideDvarName(string dvarName);

        /// <summary>
        ///     retrieves the configuration value of a dvar key for
        ///     games that do not support the given dvar
        /// </summary>
        /// <param name="dvarName">dvar key name</param>
        /// <returns></returns>
        T GetDefaultDvarValue<T>(string dvarName);

        /// <summary>
        ///     determines the amount of time to wait for the command to respond
        /// </summary>
        /// <param name="command">name of command being executed</param>
        /// <returns></returns>
        TimeSpan? OverrideTimeoutForCommand(string command);
    }
}
