using System;
using Data.Models.Client;

namespace SharedLibraryCore.Dtos.Meta.Responses;

public class PermissionLevelChangedResponse : BaseMetaResponse
{
    public EFClient.Permission PreviousPermissionLevel =>
        (EFClient.Permission)Enum.Parse(typeof(EFClient.Permission),
            PreviousPermissionLevelValue ?? EFClient.Permission.User.ToString());

    public string PreviousPermissionLevelValue { get; set; }

    public EFClient.Permission CurrentPermissionLevel => (EFClient.Permission)Enum.Parse(typeof(EFClient.Permission),
        CurrentPermissionLevelValue ?? EFClient.Permission.User.ToString());

    public string CurrentPermissionLevelValue { get; set; }
    public int ChangedById { get; set; }
    public string ChangedByName { get; set; }
}
