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

    [Header("User-Agent", "IW4MAdmin-RestEase")]
    public interface IMasterApi
    {
        [Header("Authorization")]
        string AuthorizationToken { get; set; }

        [Post("authenticate")]
        Task<TokenId> Authenticate([Body] AuthenticationId Id);

        [Post("instance/")]
        Task<ApiInstance> AddInstance([Body] ApiInstance instance);

        [Put("instance/{id}")]
        Task<ApiInstance> UpdateInstance([Path] string id, [Body] ApiInstance instance);
    }
}
