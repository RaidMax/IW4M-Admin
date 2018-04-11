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
                "nigger",
                "nigga",
                "fuck"
            };

            EnableProfanityDeterment = Utilities.PromptBool("Enable profanity deterring");
            ProfanityWarningMessage = "Please do not use profanity on this server";
            ProfanityKickMessage = "Excessive use of profanity";
            KickAfterInfringementCount = 2;

            return this;
        }

        public string Name() => "Configuration";
    }
}
