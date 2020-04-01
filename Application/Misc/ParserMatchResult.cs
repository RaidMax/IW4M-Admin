using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Misc
{
    /// <summary>
    /// implementation of the IMatchResult
    /// used to hold matching results
    /// </summary>
    public class ParserMatchResult : IMatchResult
    {
        /// <summary>
        /// array of matched pattern groups
        /// </summary>
        public string[] Values { get; set; }

        /// <summary>
        /// indicates if the match succeeded
        /// </summary>
        public bool Success { get; set; }
    }
}
