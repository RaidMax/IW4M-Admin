using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace IW4MAdmin.Application
{
    /// <summary>
    /// implementatin of IPageList that supports basic
    /// pages title and page location for webfront
    /// </summary>
    class PageList : IPageList
    {
        /// <summary>
        /// Pages dictionary
        /// Key = page name
        /// Value = page location (url)
        /// </summary>
        public IDictionary<string, string> Pages { get; set; }

        public PageList()
        {
            Pages = new Dictionary<string, string>();
        }
    }
}
