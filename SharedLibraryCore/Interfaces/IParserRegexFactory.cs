namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// defines the capabilities of the parser regex factory
    /// </summary>
    public interface IParserRegexFactory
    {
        /// <summary>
        /// creates a new ParserRegex instance
        /// </summary>
        /// <returns>ParserRegex instance</returns>
        ParserRegex CreateParserRegex();
    }
}
