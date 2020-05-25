namespace SharedLibraryCore.Dtos
{
    public class FindClientResult
    {
        /// <summary>
        /// client identifier
        /// </summary>
        public int ClientId { get; set; }

        /// <summary>
        /// networkid of client
        /// </summary>
        public string Xuid { get; set; }

        /// <summary>
        /// name of client
        /// </summary>
        public string Name { get; set; }
    }
}
