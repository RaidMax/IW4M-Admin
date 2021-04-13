using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Numerics;
using Data.Models.Client.Stats;

namespace Data.Models.Client
{
    public class EFACSnapshotVector3 : SharedEntity
    {
        [Key]
        public int ACSnapshotVector3Id { get; set; }

        public int SnapshotId { get; set; }

        [ForeignKey("SnapshotId")]
        public EFACSnapshot Snapshot { get; set; }

        public int Vector3Id { get; set; }

        [ForeignKey("Vector3Id")]
        public Vector3 Vector { get; set;}
    }
}
