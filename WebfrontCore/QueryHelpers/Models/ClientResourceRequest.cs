using System;
using Data.Models;
using Data.Models.Client;
using SharedLibraryCore.QueryHelper;

namespace WebfrontCore.QueryHelpers.Models;

public class ClientResourceRequest : ClientPaginationRequest
{
    public string ClientName { get; set; }
    public bool IsExactClientName { get; set; }
    public string ClientIp { get; set; }
    public bool IsExactClientIp { get; set; }
    public string ClientGuid { get; set; }
    public DateTime? ClientConnected { get; set; }
    public EFClient.Permission? ClientLevel { get; set; }
    public Reference.Game? GameName { get; set; }
    public bool IncludeGeolocationData { get; set; } = true;

    public bool HasData => !string.IsNullOrEmpty(ClientName) || !string.IsNullOrEmpty(ClientIp) ||
                           !string.IsNullOrEmpty(ClientGuid) || ClientLevel is not null || GameName is not null;
}
