namespace SharedLibraryCore.Commands
{
    /// <summary>
    /// Holds information about command args
    /// </summary>
    public class CommandArgument
    {
        /// <summary>
        /// Name of the argument
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Indicates if the argument is required
        /// </summary>
        public bool Required { get; set; }
    }
}
