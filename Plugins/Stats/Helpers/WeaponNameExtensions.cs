using Data.Models.Client.Stats;

namespace Stats.Helpers
{
    public static class WeaponNameExtensions
    {
        public static string RebuildWeaponName(this EFClientHitStatistic stat) =>
            $"{stat.Weapon?.Name}{string.Join("_", stat.WeaponAttachmentCombo?.Attachment1?.Name, stat.WeaponAttachmentCombo?.Attachment2?.Name, stat.WeaponAttachmentCombo?.Attachment3?.Name)}";
    }
}