namespace SharedLibraryCore.Dtos
{
    /// <summary>
    ///     pagination information holder class
    /// </summary>
    public class PaginationRequest
    {
        /// <summary>
        ///     how many items to skip
        /// </summary>
        public int Offset { get; set; }

        public int Count
        {
            get;
            set => field = Math.Min(value, 100);
        } = 50;

        /// <summary>
        ///     filter query
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        ///     direction of ordering
        /// </summary>
        public SortDirection Direction { get; set; } = SortDirection.Descending;

        public string SortColumn { get; set; }

        public DateTime? Before { get; set; }

        public DateTime? After { get; set; }
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }
}
