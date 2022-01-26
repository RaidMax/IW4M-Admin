namespace SharedLibraryCore.Dtos
{
    public class FindClientRequest : PaginationRequest
    {
        /// <summary>
        ///     name of client
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        ///     network id of client
        /// </summary>
        public string Xuid { get; set; }

        public string ToDebugString()
        {
            return $"[Name={Name}, Xuid={Xuid}]";
        }
    }
}