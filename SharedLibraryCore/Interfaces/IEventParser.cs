﻿using System;
using static SharedLibraryCore.Server;

namespace SharedLibraryCore.Interfaces
{
    public interface IEventParser
    {
        /// <summary>
        ///     Get game specific folder prefix for log files
        /// </summary>
        /// <returns>Game directory prefix</returns>
        IEventParserConfiguration Configuration { get; set; }

        /// <summary>
        ///     stores the game/client specific version (usually the value of the "version" DVAR)
        /// </summary>
        string Version { get; set; }

        /// <summary>
        ///     specifies the game name (usually the internal studio iteration ie: IW4, T5 etc...)
        /// </summary>
        Game GameName { get; set; }

        /// <summary>
        ///     specifies the connect URI used to join game servers via web browser
        /// </summary>
        string URLProtocolFormat { get; set; }

        /// <summary>
        ///     specifies the text name of the game the parser is for
        /// </summary>
        string Name { get; set; }

        /// <summary>
        ///     Generates a game event based on log line input
        /// </summary>
        /// <param name="logLine">single log line string</param>
        /// <returns></returns>
        /// todo: make this integrate without needing the server
        GameEvent GenerateGameEvent(string logLine);

        /// <summary>
        ///     registers a custom event subtype to be triggered when a value is detected
        /// </summary>
        /// <param name="eventSubtype">subtype assigned to the event when generated</param>
        /// <param name="eventTriggerValue">event keyword to trigger an event generation</param>
        /// <param name="eventModifier">function pointer that modifies the generated game event</param>
        void RegisterCustomEvent(string eventSubtype, string eventTriggerValue,
            Func<string, IEventParserConfiguration, GameEvent, GameEvent> eventModifier);
    }
}