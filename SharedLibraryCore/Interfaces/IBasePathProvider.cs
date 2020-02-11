namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// defines the capabilities for providing a base path
    /// unused as of now, will be used later during refactorying
    /// </summary>
    public interface IBasePathProvider
    {
        /// <summary>
        /// working directory of IW4MAdmin
        /// </summary>
        string BasePath { get; }
    }
}
