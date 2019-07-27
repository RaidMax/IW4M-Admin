using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace IW4MAdmin.Application.Misc
{
    class MiddlewareActionHandler : IMiddlewareActionHandler
    {
        private static readonly IDictionary<string, IList<object>> _actions = new Dictionary<string, IList<object>>();

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
                    // todo: probably log this somewhere
                    catch { }
                }

                return value;
            }

            return value;
        }

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
