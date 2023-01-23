namespace WebfrontCore.Permissions;

public enum WebfrontEntity
{
    ClientIPAddress,
    ClientGuid,
    ClientLevel,
    MetaAliasUpdate,
    Penalty,
    PrivilegedClientsPage,
    HelpPage,
    ConsolePage,
    ConfigurationPage,
    AuditPage,
    RecentPlayersPage,
    ProfilePage,
    AdminMenu,
    ClientNote,
    Interaction,
    AdvancedSearch
}

public enum WebfrontPermission
{
    Read,
    Write,
    Delete
}
