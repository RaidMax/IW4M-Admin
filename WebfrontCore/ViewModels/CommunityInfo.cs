using System.Collections.Generic;
using SharedLibraryCore.Configuration;

namespace WebfrontCore.ViewModels
{
    public class CommunityInfo
    {
        public string[] GlobalRules { get; set; }
        public Dictionary<string, string[]> ServerRules { get; set; }
        public CommunityInformationConfiguration CommunityInformation { get; set; }
    }
}