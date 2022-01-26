using System.Collections.Generic;

namespace SharedLibraryCore.Interfaces
{
    public interface IPageList
    {
        IDictionary<string, string> Pages { get; set; }
    }
}