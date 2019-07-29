using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// used to handle middleware actions registered from arbitrary assemblies
    /// </summary>
    public interface IMiddlewareActionHandler
    {
        /// <summary>
        /// registers an action with the middleware handler
        /// </summary>
        /// <typeparam name="T">action return type</typeparam>
        /// <param name="actionType">class type of action</param>
        /// <param name="action">action to perform</param>
        /// <param name="name">optional name to reference the action by</param>
        void Register<T>(T actionType, IMiddlewareAction<T> action, string name = null);

        /// <summary>
        /// executes the given action type or name
        /// </summary>
        /// <typeparam name="T">action return type</typeparam>
        /// <param name="value">instance member to perform the action on</param>
        /// <param name="name">optional name to reference the action by</param>
        /// <returns></returns>
        Task<T> Execute<T>(T value, string name = null);
    }
}
