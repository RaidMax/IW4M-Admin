using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibraryCore.Interfaces
{
    public interface IMiddlewareAction<T>
    {
        Task<T> Invoke(T original);
    }
}
