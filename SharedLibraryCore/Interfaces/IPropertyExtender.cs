namespace SharedLibraryCore.Interfaces
{
    /// <summary>
    /// describes the capability of extending properties by name
    /// </summary>
    interface IPropertyExtender
    {
        /// <summary>
        /// adds or updates property by name
        /// </summary>
        /// <param name="name">unique name of the property</param>
        /// <param name="value">value of the property</param>
        void SetAdditionalProperty(string name, object value);

        /// <summary>
        /// retreives a property by name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">name of the property</param>
        /// <returns>property value if exists, otherwise default T</returns>
        T GetAdditionalProperty<T>(string name);
    }
}
