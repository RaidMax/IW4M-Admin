using SharedLibraryCore.Dtos;

namespace SharedLibraryCore.Dtos
{
    public class FindClientResult
    {
        /// <summary>
        ///     client identifier
        /// </summary>
        public int ClientId { get; set; }

        /// <summary>
        ///     networkid of client
        /// </summary>
        public string Xuid { get; set; }

        /// <summary>
        ///     name of client
        /// </summary>
        public string Name { get; set; }
        
        public Data.Models.Client.EFClient.Permission Level { get; set; }
        public System.DateTime LastConnection { get; set; }
    }
}