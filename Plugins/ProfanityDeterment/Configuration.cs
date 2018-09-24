using System.Collections.Generic;
using SharedLibraryCore;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Plugins.ProfanityDeterment
{
    class Configuration : IBaseConfiguration
    {
        public List<string> OffensiveWords { get; set; }
        public bool EnableProfanityDeterment { get; set; }
        public string ProfanityWarningMessage { get; set; }
        public string ProfanityKickMessage { get; set; }
        public int KickAfterInfringementCount { get; set; }

        public IBaseConfiguration Generate()
        {
            OffensiveWords = new List<string>()
            {
                @"\s*n+.*i+.*g+.*e+.*r+\s*",
                @"\s*n+.*i+.*g+.*a+\s*",
                @"\s*f+u+.*c+.*k+.*\s*"
            };

            var loc = Utilities.CurrentLocalization.LocalizationIndex;

            EnableProfanityDeterment = Utilities.PromptBool(loc["PLUGINS_PROFANITY_SETUP_ENABLE"]);
            ProfanityWarningMessage = loc["PLUGINS_PROFANITY_WARNMSG"];
            ProfanityKickMessage = loc["PLUGINS_PROFANITY_KICKMSG"];
            KickAfterInfringementCount = 2;

            return this;
        }

        public string Name() => "Configuration";
    }
}
