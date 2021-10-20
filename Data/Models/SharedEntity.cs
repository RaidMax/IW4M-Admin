using Data.Abstractions;
using System.Collections.Concurrent;

namespace Data.Models
{
    public class SharedEntity : IPropertyExtender
    {
        private ConcurrentDictionary<string, object> _additionalProperties;

        /// <summary>
        /// indicates if the entity is active
        /// </summary>
        public bool Active { get; set; } = true;

        public SharedEntity()
        {
            _additionalProperties = new ConcurrentDictionary<string, object>();
        }

        public T GetAdditionalProperty<T>(string name)
        {
            return _additionalProperties.ContainsKey(name) ? (T)_additionalProperties[name] : default;
        }

        public void SetAdditionalProperty(string name, object value)
        {
            if (_additionalProperties.ContainsKey(name))
            {
                _additionalProperties[name] = value;
            }
            else
            {
                _additionalProperties.TryAdd(name, value);
            }
        }
        
        public void CopyAdditionalProperties(SharedEntity source)
        {
            _additionalProperties = source._additionalProperties;
        }
    }
}
