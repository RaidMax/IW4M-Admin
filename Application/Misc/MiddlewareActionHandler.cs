using SharedLibraryCore;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IW4MAdmin.Application.Misc
{
    class MiddlewareActionHandler : IMiddlewareActionHandler
    {
        private readonly IDictionary<string, IList<object>> _actions;
        private readonly ILogger _logger;

        public MiddlewareActionHandler(ILogger logger)
        {
            _actions = new Dictionary<string, IList<object>>();
            _logger = logger;
        }

        /// <summary>
        /// Executes the action with the given name
        /// </summary>
        /// <typeparam name="T">Execution return type</typeparam>
        /// <param name="value">Input value</param>
        /// <param name="name">Name of action to execute</param>
        /// <returns></returns>
        public async Task<T> Execute<T>(T value, string name = null)
        {
            string key = string.IsNullOrEmpty(name) ? typeof(T).ToString() : name;

            if (_actions.ContainsKey(key))
            {
                foreach (var action in _actions[key])
                {
                    try
                    {
                        value = await ((IMiddlewareAction<T>)action).Invoke(value);
                    }
                    catch (Exception e) 
                    {
                        _logger.WriteWarning($"Failed to invoke middleware action {name}");
                        _logger.WriteDebug(e.GetExceptionInfo());
                    }
                }

                return value;
            }

            return value;
        }

        /// <summary>
        /// Registers an action by name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="actionType">Action type specifier</param>
        /// <param name="action">Action to perform</param>
        /// <param name="name">Name of action</param>
        public void Register<T>(T actionType, IMiddlewareAction<T> action, string name = null)
        {
            string key = string.IsNullOrEmpty(name) ? typeof(T).ToString() : name;

            if (_actions.ContainsKey(key))
            {
                _actions[key].Add(action);
            }

            else
            {
                _actions.Add(key, new[] { action });
            }
        }
    }
}
