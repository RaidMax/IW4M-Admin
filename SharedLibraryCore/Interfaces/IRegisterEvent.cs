using System;
using System.Collections.Generic;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// interface defining the capabilities of a custom event registration
    /// </summary>
    public interface IRegisterEvent
    {
        /// <summary>
        /// collection of custom event registrations
        /// <remarks>
        /// (Subtype, trigger value, event generator)
        /// </remarks>
        /// </summary>
        IEnumerable<(string, string, Func<string, IEventParserConfiguration, GameEvent, GameEvent>)> Events { get; }
    }
}
