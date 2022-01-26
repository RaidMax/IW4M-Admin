﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Data.Models.Client;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;
using static SharedLibraryCore.Server;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace SharedLibraryCore
{
    /// <summary>
    ///     Abstract class for command
    /// </summary>
    public abstract class Command : IManagerCommand
    {
        protected readonly CommandConfiguration _config;
        protected readonly ITranslationLookup _translationLookup;
        private string alias;
        protected ILogger logger;
        private string name;
        private EFClient.Permission permission;
        private Game[] supportedGames;

        public Command(CommandConfiguration config, ITranslationLookup layout)
        {
            _config = config;
            _translationLookup = layout;
            SupportedGames = new Game[0];
        }

        /// <summary>
        ///     Helper property to determine the number of required args
        /// </summary>
        public int RequiredArgumentCount => Arguments.Count(c => c.Required);


        /// <summary>
        ///     Argument list for the command
        /// </summary>
        public CommandArgument[] Arguments { get; protected set; } = new CommandArgument[0];

        /// <summary>
        ///     Executes the command
        /// </summary>
        /// <param name="gameEvent"></param>
        /// <returns></returns>
        public abstract Task ExecuteAsync(GameEvent gameEvent);

        /// <summary>
        ///     Specifies the name and string that triggers the command
        /// </summary>
        public string Name
        {
            get => name;
            protected set
            {
                try
                {
                    name = _config?.Commands[GetType().Name].Name ?? value;
                }

                catch (KeyNotFoundException)
                {
                    name = value;
                }
            }
        }

        /// <summary>
        ///     Specifies the command description
        /// </summary>
        public string Description { get; protected set; }

        /// <summary>
        ///     Helper property to provide the syntax of the command
        /// </summary>
        public string Syntax =>
            $"{_translationLookup["COMMAND_HELP_SYNTAX"]} {_config.CommandPrefix ?? "!"}{Alias} {string.Join(" ", Arguments.Select(a => $"<{(a.Required ? "" : _translationLookup["COMMAND_HELP_OPTIONAL"] + " ")}{a.Name}>"))}";

        /// <summary>
        ///     Alternate name for this command to be executed by
        /// </summary>
        public string Alias
        {
            get => alias;
            protected set
            {
                try
                {
                    alias = _config?.Commands[GetType().Name].Alias ?? value;
                }

                catch (KeyNotFoundException)
                {
                    alias = value;
                }
            }
        }

        /// <summary>
        ///     Indicates if the command requires a target to execute on
        /// </summary>
        public bool RequiresTarget { get; protected set; }

        /// <summary>
        ///     Minimum permission level to execute command
        /// </summary>
        public EFClient.Permission Permission
        {
            get => permission;
            protected set
            {
                try
                {
                    permission = _config?.Commands[GetType().Name].MinimumPermission ?? value;
                }

                catch (KeyNotFoundException)
                {
                    permission = value;
                }
            }
        }

        public Game[] SupportedGames
        {
            get => supportedGames;
            protected set
            {
                try
                {
                    var savedGames = _config?.Commands[GetType().Name].SupportedGames;
                    supportedGames = savedGames?.Length != 0 ? savedGames : value;
                }

                catch (KeyNotFoundException)
                {
                    supportedGames = value;
                }
            }
        }

        /// <summary>
        ///     indicates if this command allows impersonation (run as)
        /// </summary>
        public bool AllowImpersonation { get; set; }

        public bool IsBroadcast { get; set; }
    }
}