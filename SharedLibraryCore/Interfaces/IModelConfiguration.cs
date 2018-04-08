using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Interfaces
{
    public interface IModelConfiguration
    {
        void Configure(ModelBuilder builder);
    }
}
