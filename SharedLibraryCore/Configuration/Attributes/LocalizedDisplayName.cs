using System.ComponentModel;

namespace SharedLibraryCore.Configuration.Attributes
{
    internal class LocalizedDisplayName : DisplayNameAttribute
    {
        private readonly string _localizationKey;

        public LocalizedDisplayName(string localizationKey)
        {
            _localizationKey = localizationKey;
        }

        public override string DisplayName => Utilities.CurrentLocalization.LocalizationIndex[_localizationKey];
    }
}