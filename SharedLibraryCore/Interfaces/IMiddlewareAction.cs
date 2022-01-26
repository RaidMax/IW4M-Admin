using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    ///     represents an invokable middleware action
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public interface IMiddlewareAction<T>
    {
        /// <summary>
        ///     action to execute when the middleware action is invoked
        /// </summary>
        /// <param name="original"></param>
        /// <returns>modified original action type instance</returns>
        Task<T> Invoke(T original);
    }
}