using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Interfaces
{
    public interface IClientMetaResponse
    {
        int ClientId { get;}
        long MetaId { get; }
    }
}
