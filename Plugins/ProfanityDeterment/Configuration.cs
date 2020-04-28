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
                @"(ph|f)[a@]g[s\$]?",
                @"(ph|f)[a@]gg[i1]ng",
                @"(ph|f)[a@]gg?[o0][t\+][s\$]?",
                @"(ph|f)[a@]gg[s\$]",
                @"(ph|f)[e3][l1][l1]?[a@][t\+][i1][o0]",
                @"(ph|f)u(c|k|ck|q)",
                @"(ph|f)u(c|k|ck|q)[s\$]?",
                @"(c|k|ck|q)un[t\+][l1][i1](c|k|ck|q)",
                @"(c|k|ck|q)un[t\+][l1][i1](c|k|ck|q)[e3]r",
                @"(c|k|ck|q)un[t\+][l1][i1](c|k|ck|q)[i1]ng",
                @"b[i1][t\+]ch[s\$]?",
                @"b[i1][t\+]ch[e3]r[s\$]?",
                @"b[i1][t\+]ch[e3][s\$]",
                @"b[i1][t\+]ch[i1]ng?",
                @"n[i1]gg?[e3]r[s\$]?",
                @"[s\$]h[i1][t\+][s\$]?",
                @"[s\$][l1]u[t\+][s\$]?"
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
