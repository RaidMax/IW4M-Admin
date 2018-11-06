namespace SharedLibraryCore.Dtos
{
    public class ClientInfo
    {
        public string Name { get; set; }
        public int ClientId { get; set; }
        public int LinkId { get; set; }
        public Database.Models.EFClient.Permission Level { get; set; }
    }
}
