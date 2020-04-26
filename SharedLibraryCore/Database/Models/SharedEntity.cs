using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibraryCore.Database.Models
{
    public class SharedEntity : IPropertyExtender
    {
        private readonly ConcurrentDictionary<string, object> _additionalProperties;

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

        ///// <summary>
        ///// Specifies when the entity was created
        ///// </summary>
        //[Column(TypeName="datetime")]
        //public DateTime CreatedDateTime { get; set; }

        ///// <summary>
        ///// Specifies when the entity was updated
        ///// </summary>
        //[Column(TypeName = "datetime")]
        //public DateTime? UpdatedDateTime { get;set; }
    }
}
