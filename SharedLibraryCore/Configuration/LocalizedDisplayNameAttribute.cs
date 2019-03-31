using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

namespace SharedLibraryCore.Configuration
{
    class LocalizedDisplayNameAttribute : DisplayNameAttribute
    {
        private readonly string _localizationKey;
        public LocalizedDisplayNameAttribute(string localizationKey)
        {
            _localizationKey = localizationKey;
        }

        public override string DisplayName => Utilities.CurrentLocalization.LocalizationIndex[_localizationKey];
    }
}
