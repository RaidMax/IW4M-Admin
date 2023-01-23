using System;
using Data.Models;
using Data.Models.Client;

namespace WebfrontCore.QueryHelpers.Models;

public class ClientResourceResponse
{
    public int ClientId { get; set; }
    public int AliasId { get; set; }
    public int LinkId { get; set; }
    public string CurrentClientName { get; set; }
    public string MatchedClientName { get; set; }
    public int? CurrentClientIp { get; set; }
    public int? MatchedClientIp { get; set; }
    public string ClientCountryCode { get; set; }
    public string ClientCountryDisplayName { get; set; }
    public string ClientLevel { get; set; }
    public EFClient.Permission ClientLevelValue { get; set; }
    public DateTime LastConnection { get; set; }
    public Reference.Game Game { get; set; }
}
