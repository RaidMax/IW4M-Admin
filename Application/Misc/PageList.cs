using SharedLibraryCore.Interfaces;
using System.Collections.Generic;

namespace IW4MAdmin.Application
{
    /// <summary>
    /// implementation of IPageList that supports a page title, page location,
    /// and an optional navbar icon for the webfront
    /// </summary>
    class PageList : IPageList
    {
        /// <summary>
        /// Pages dictionary
        /// Key = page name
        /// Value = page location (url)
        /// </summary>
        public IDictionary<string, string> Pages { get; set; }

        /// <summary>
        /// Optional navbar icon per page name (Phosphor icon class, e.g. "ph-detective").
        /// </summary>
        public IDictionary<string, string> PageIcons { get; }

        public PageList()
        {
            Pages = new Dictionary<string, string>();
            PageIcons = new Dictionary<string, string>();
        }

        public void AddPage(string name, string location, string icon = null)
        {
            Pages[name] = location;

            if (!string.IsNullOrWhiteSpace(icon))
            {
                PageIcons[name] = icon;
            }
        }
    }
}
