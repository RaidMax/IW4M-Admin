using System.Collections.Generic;

namespace SharedLibraryCore.Helpers
{
    /// <summary>
    /// generic class for passing information about a resource query
    /// </summary>
    /// <typeparam name="QueryResultType">Type of query result</typeparam>
    public class ResourceQueryHelperResult<QueryResultType>
    {
        /// <summary>
        /// indicates the total number of results found
        /// </summary>
        public long TotalResultCount { get; set; }

        /// <summary>
        /// indicates the total number of results retrieved
        /// </summary>
        public int RetrievedResultCount { get; set; }

        /// <summary>
        /// collection of results
        /// </summary>
        public IEnumerable<QueryResultType> Results { get; set; }
    }
}
