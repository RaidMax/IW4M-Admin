using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace SharedLibraryCore.Helpers
{
    /// <summary>
    /// This class provides a way to keep track of changes to an entity
    /// </summary>
    /// <typeparam name="T">Type of entity to keep track of changes to</typeparam>
    public class ChangeTracking<T>
    {
        List<T> Values;

        public ChangeTracking()
        {
            Values = new List<T>();
        }

        public void OnChange(T value)
        {
            // clear the first value when count max count reached
            if (Values.Count > 30)
                Values.RemoveAt(0);
            Values.Add(value);
        }

        public List<T> GetChanges() => Values;
    }
}
