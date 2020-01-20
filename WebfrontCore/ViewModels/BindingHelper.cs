namespace WebfrontCore.ViewModels
{
    /// <summary>
    /// Helper class that hold information to assist with binding lists of items
    /// </summary>
    public class BindingHelper
    {
        /// <summary>
        /// Sequential property mapping items
        /// </summary>
        public string[] Properties { get; set; }
        
        /// <summary>
        /// Index in the array this new item lives
        /// </summary>
        public int ItemIndex { get; set; }

        /// <summary>
        /// Index in the array of the parent item
        /// </summary>
        public int ParentItemIndex { get; set; }
    }
}
