namespace SharedLibraryCore.Dtos
{
    public class Page
    {
        public string Name { get; set; }
        public string Location { get; set; }

        /// <summary>
        /// Optional navbar icon, as a Phosphor icon class (e.g. "ph-detective").
        /// When null/empty the webfront falls back to its default page icon.
        /// </summary>
        public string IconId { get; set; }
    }
}
