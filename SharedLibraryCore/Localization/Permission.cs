using static SharedLibraryCore.Database.Models.EFClient;

namespace SharedLibraryCore.Localization
{
    public sealed class ClientPermission
    {
        public Permission Level { get; set; }
        public string Name { get; set; }
    }
}
