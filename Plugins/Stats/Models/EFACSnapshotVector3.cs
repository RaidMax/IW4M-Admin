using IW4MAdmin.Plugins.Stats.Models;
using SharedLibraryCore.Helpers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace IW4MAdmin.Plugins.Stats.Models
{
    public class EFACSnapshotVector3
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
