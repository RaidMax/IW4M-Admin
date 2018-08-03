using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Interfaces
{
    public interface IPageList
    {
        IDictionary<string, string> Pages { get; set; }
    }
}
