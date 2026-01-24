namespace WebfrontCore.Core.Auth;

public enum WebfrontEntity
{
    Default,
    ClientIPAddress,
    ClientGuid,
    ClientLevel,
    MetaAliasUpdate,
    Penalty,
    PrivilegedClientsPage,
    HelpPage,
    ConsolePage,
    AuditPage,
    RecentPlayersPage,
    ProfilePage,
    BanManagementPage,
    AdminMenu,
    ClientNote,
    Interaction,
    AdvancedSearch,
    AuditLogDataDetails,
    AnnouncementPage,
    Announcement,
    ChatMessage
}

public enum WebfrontPermission
{
    Read,
    Write,
    Delete
}
