using System;
using System.ComponentModel.DataAnnotations.Schema;

namespace SharedLibraryCore.Database.Models
{
    public class SharedEntity
    {
        /// <summary>
        /// indicates if the entity is active
        /// </summary>
        public bool Active { get; set; } = true;

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
