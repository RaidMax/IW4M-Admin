using System.Collections.Concurrent;

namespace SharedLibraryCore.Helpers
{
    /// <summary>
    ///     This class provides a way to keep track of changes to an entity
    /// </summary>
    /// <typeparam name="T">Type of entity to keep track of changes to</typeparam>
    public class ChangeTracking<T>
    {
        private readonly ConcurrentQueue<T> Values;

        public ChangeTracking()
        {
            Values = new ConcurrentQueue<T>();
        }

        public bool HasChanges => Values.Count > 0;

        public void OnChange(T value)
        {
            if (Values.Count > 30)
            {
                Values.TryDequeue(out var throwAway);
            }

            Values.Enqueue(value);
        }

        public T GetNextChange()
        {
            var itemDequeued = Values.TryDequeue(out var val);
            return itemDequeued ? val : default;
        }
    }
}