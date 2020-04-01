namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// represents a pattern match result
    /// </summary>
    public interface IMatchResult
    {
        /// <summary>
        /// array of matched pattern groups
        /// </summary>
        string[] Values { get; set; }

        /// <summary>
        /// indicates if the match succeeded
        /// </summary>
        bool Success { get; set; }
    }
}
