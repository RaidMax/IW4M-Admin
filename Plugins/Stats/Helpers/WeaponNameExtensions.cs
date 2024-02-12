using System.Linq;
using Data.Models.Client.Stats;

namespace Stats.Helpers
{
    public static class WeaponNameExtensions
    {
        public static string RebuildWeaponName(this EFClientHitStatistic stat)
        {
            var attachments =
                new[]
                {
                    stat.WeaponAttachmentCombo?.Attachment1?.Name, stat.WeaponAttachmentCombo?.Attachment2?.Name,
                    stat.WeaponAttachmentCombo?.Attachment3?.Name
                }.Where(a => !string.IsNullOrEmpty(a));

            return $"{stat.Weapon?.Name?.Replace("zombie_", "").Replace("_zombie", "")}{string.Join("_", attachments)}";
        }
    }
}
