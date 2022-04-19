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
    AdminMenu
}

public enum WebfrontPermission
{
    Read,
    Create,
    Update,
    Delete
}
