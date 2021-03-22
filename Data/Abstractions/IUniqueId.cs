using System.ComponentModel.DataAnnotations.Schema;

namespace Data.Abstractions
{
    public interface IUniqueId
    {
        [NotMapped]
        long Id { get; }

        [NotMapped]
        string Value { get; }
    }
}
