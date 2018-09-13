using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Events
{
    /// <summary>
    /// represents change from one value to another
    /// </summary>
    class Change
    {
        /// <summary>
        /// represents the previous value of the item
        /// </summary>
        public string PreviousValue { get; set; }
        /// <summary>
        /// represents the new/current value of the item
        /// </summary>
        public string NewValue { get; set; }
    }
}
