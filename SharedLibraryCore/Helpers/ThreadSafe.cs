using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Helpers
{
    /// <summary>
    /// Excuse this monstrosity
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ThreadSafe<T>
    {
        private bool _lock;
        private T instance;

        public ThreadSafe(T instance)
        {
            this.instance = instance;
            _lock = true;
        }

        public T Value
        {
            get
            {
                // shush
                if (_lock)
                    return Value;
                _lock = true;
                return instance;
            }

            set
            {
                if (_lock)
                {
                    Value = Value;
                    return;
                }
                instance = Value;
            }
        }


    }
}
