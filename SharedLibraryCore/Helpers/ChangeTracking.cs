using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
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
        ConcurrentQueue<T> Values;

        public ChangeTracking()
        {
            Values = new ConcurrentQueue<T>();
        }

        public void OnChange(T value)
        {
            if (Values.Count > 30)
                Values.TryDequeue(out T throwAway);
            Values.Enqueue(value);
        }

        public T GetNextChange()
        {
            bool itemDequeued = Values.TryDequeue(out T val);
            return itemDequeued ? val : default(T);
        }

        public bool HasChanges => Values.Count > 0;
    }
}
