using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using RestEase;

namespace IW4MAdmin.Application.API.Master
{
    public class AuthenticationId
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

    public class TokenId
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
    }

    public class VersionInfo
    {
        [JsonProperty("current-version-stable")]
        public float CurrentVersionStable { get; set; }
        [JsonProperty("current-version-prerelease")]
        public float CurrentVersionPrerelease { get; set; }
    }

    public class ResultMessage
    {
        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class Endpoint
    {
#if !DEBUG
        private static IMasterApi api = RestClient.For<IMasterApi>("http://api.raidmax.org:5000");
#else
        private static IMasterApi api = RestClient.For<IMasterApi>("http://127.0.0.1");
#endif
        public static IMasterApi Get() => api;
    }

    [Header("User-Agent", "IW4MAdmin-RestEase")]
    public interface IMasterApi
    {
        [Header("Authorization")]
        string AuthorizationToken { get; set; }

        [Post("authenticate")]
        Task<TokenId> Authenticate([Body] AuthenticationId Id);

        [Post("instance/")]
        Task<ResultMessage> AddInstance([Body] ApiInstance instance);

        [Put("instance/{id}")]
        Task<ResultMessage> UpdateInstance([Path] string id, [Body] ApiInstance instance);

        [Get("version")]
        Task<VersionInfo> GetVersion();

        [Get("localization")]
        Task<List<SharedLibraryCore.Localization.Layout>> GetLocalization();

        [Get("localization/{languageTag}")]
        Task<SharedLibraryCore.Localization.Layout> GetLocalization([Path("languageTag")] string languageTag);
    }
}
