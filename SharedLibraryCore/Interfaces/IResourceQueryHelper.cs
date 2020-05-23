using SharedLibraryCore.Helpers;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// defines the capabilities of a resource queryier
    /// </summary>
    /// <typeparam name="QueryType">Type of query</typeparam>
    /// <typeparam name="ResultType">Type of result</typeparam>
    public interface IResourceQueryHelper<QueryType, ResultType>
    {
        /// <summary>
        /// queries a resource and returns the result of the query
        /// </summary>
        /// <param name="query">query params</param>
        /// <returns></returns>
        Task<ResourceQueryHelperResult<ResultType>> QueryResource(QueryType query);
    }
}
