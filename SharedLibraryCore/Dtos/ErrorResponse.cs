namespace SharedLibraryCore.Dtos
{
    public class ErrorResponse
    {
        /// <summary>
        /// todo: type of error
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// relevant error messages
        /// </summary>
        public string[] Messages { get; set; }
    }
}
