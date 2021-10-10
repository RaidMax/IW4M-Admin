using SharedLibraryCore.Configuration;

namespace WebfrontCore.Extensions
{
    public static class WebfrontExtensions
    {
        public static bool ShouldUseFallbackBranding(this ApplicationConfiguration appConfig) =>
            string.IsNullOrWhiteSpace(appConfig?.CommunityInformation?.Name) || appConfig.CommunityInformation.Name.Contains("IW4MAdmin");
    }
}