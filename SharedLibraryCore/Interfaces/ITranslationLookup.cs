namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// Defines the translation lookup capabilities for DI
    /// </summary>
    public interface ITranslationLookup
    {
        /// <summary>
        /// Allows indexing
        /// </summary>
        /// <param name="key">translation lookup key</param>
        /// <returns></returns>
        string this[string key] { get; }
    }
}
