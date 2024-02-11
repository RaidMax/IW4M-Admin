using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using IW4MAdmin.Application.Plugin;
using Refit;
using SharedLibraryCore.Helpers;

namespace IW4MAdmin.Application.API.Master;

public class AuthenticationId
{
    [JsonPropertyName("id")] public string Id { get; set; }
}

public class TokenId
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; }
}

public class VersionInfo
{
    [JsonPropertyName("current-version-stable")]
    [JsonConverter(typeof(BuildNumberJsonConverter))]
    public BuildNumber CurrentVersionStable { get; set; }

    [JsonPropertyName("current-version-prerelease")]
    [JsonConverter(typeof(BuildNumberJsonConverter))]
    public BuildNumber CurrentVersionPrerelease { get; set; }
}

public class ResultMessage
{
    [JsonPropertyName("message")] public string Message { get; set; }
}

public class PluginSubscriptionContent
{
    public string Content { get; set; }
    public PluginType Type { get; set; }
}

/// <summary>
/// Defines the capabilities of the master API
/// </summary>
[Headers("User-Agent: IW4MAdmin-RestEase")]
public interface IMasterApi
{
    [Post("/authenticate")]
    Task<TokenId> Authenticate([Body] AuthenticationId Id);

    [Post("/instance/")]
    Task<IApiResponse<ResultMessage>> AddInstance([Body] ApiInstance instance, [Header("Authorization")] string authorization);

    [Put("/instance/{id}")]
    Task<IApiResponse<ResultMessage>> UpdateInstance(string id, [Body] ApiInstance instance, [Header("Authorization")] string authorization);

    [Get("/version/{apiVersion}")]
    Task<VersionInfo> GetVersion(int apiVersion);

    [Get("/localization")]
    Task<List<SharedLibraryCore.Localization.Layout>> GetLocalization();

    [Get("/localization/{languageTag}")]
    Task<SharedLibraryCore.Localization.Layout> GetLocalization(string languageTag);

    [Get("/plugin_subscriptions")]
    Task<IEnumerable<PluginSubscriptionContent>> GetPluginSubscription([Query("instance_id")] Guid instanceId,
        [Query("subscription_id")] string subscription_id);
}
