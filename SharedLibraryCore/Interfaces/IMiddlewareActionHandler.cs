using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    public interface IMiddlewareActionHandler
    {
        void Register<T>(T actionType, IMiddlewareAction<T> action, string name = null);
        Task<T> Execute<T>(T value, string name = null);
    }
}
