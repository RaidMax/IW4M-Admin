using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SharedLibraryCore.Configuration.Attributes
{
    class LocalizedDisplayName : DisplayNameAttribute
    {
        private readonly string _localizationKey;
        public LocalizedDisplayName(string localizationKey)
        {
            _localizationKey = localizationKey;
        }

        public override string DisplayName => Utilities.CurrentLocalization.LocalizationIndex[_localizationKey];
    }
}
