namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     defines the capabilities of a parser pattern
    /// </summary>
    public interface IParserPatternMatcher
    {
        /// <summary>
        ///     converts input string into pattern groups
        /// </summary>
        /// <param name="input">input string</param>
        /// <returns>group matches</returns>
        IMatchResult Match(string input);

        /// <summary>
        ///     compiles the pattern to be used for matching
        /// </summary>
        /// <param name="pattern"></param>
        void Compile(string pattern);
    }
}