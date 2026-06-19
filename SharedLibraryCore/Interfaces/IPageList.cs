using System.Collections.Generic;

namespace SharedLibraryCore.Interfaces
{
    public interface IPageList
    {
        /// <summary>
        /// Registered navbar pages. Key = page name, value = page location (url).
        /// </summary>
        IDictionary<string, string> Pages { get; set; }

        /// <summary>
        /// Optional navbar icon per page name, as a Phosphor icon class (e.g. "ph-detective").
        /// Populated by <see cref="AddPage"/>; pages absent from this map use the webfront default icon.
        /// </summary>
        IDictionary<string, string> PageIcons { get; }

        /// <summary>
        /// Registers a navbar page with an optional icon.
        /// </summary>
        /// <param name="name">Display name shown in the navbar.</param>
        /// <param name="location">Page location (url).</param>
        /// <param name="icon">Optional Phosphor icon class (e.g. "ph-detective"). When null/empty the
        /// webfront uses its default page icon.</param>
        void AddPage(string name, string location, string icon = null);
    }
}
