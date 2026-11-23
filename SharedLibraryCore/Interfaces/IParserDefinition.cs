namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     declares a game parser (RCon + event) that the host materializes and registers
    ///     with the manager during plugin discovery.
    ///     <para>
    ///     A parser definition only describes configuration; it never constructs or registers
    ///     the parser itself. The host generates the concrete <see cref="IRConParser" /> and
    ///     <see cref="IEventParser" /> instances and passes them to <see cref="Configure" />.
    ///     This keeps parsers distinct from plugins (<see cref="IPluginV2" />) — a parser has
    ///     no lifecycle, no DI surface, and no host coupling beyond these interfaces.
    ///     </para>
    /// </summary>
    public interface IParserDefinition
    {
        /// <summary>
        ///     name of the parser, used as its registration key (replaces any existing parser
        ///     with the same name on reload).
        /// </summary>
        string Name { get; }

        /// <summary>
        ///     applies game-specific configuration to the host-supplied parser instances.
        /// </summary>
        /// <param name="rconParser">RCon parser to configure</param>
        /// <param name="eventParser">event parser to configure</param>
        void Configure(IRConParser rconParser, IEventParser eventParser);
    }
}
