using System;

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

        /// <summary>
        ///     how many itesm to take
        /// </summary>
        public int Count { get; set; } = 100;

        /// <summary>
        ///     filter query
        /// </summary>
        public string Filter { get; set; }

        /// <summary>
        ///     direction of ordering
        /// </summary>
        public SortDirection Direction { get; set; } = SortDirection.Descending;

        public DateTime? Before { get; set; }
    }

    public enum SortDirection
    {
        Ascending,
        Descending
    }
}