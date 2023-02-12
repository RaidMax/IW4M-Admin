namespace SharedLibraryCore.Events.Management;

public class LoginEvent : ManagementEvent
{
    public enum LoginSourceType
    {
        Ingame,
        Webfront
    }
    
    public string EntityId { get; init; }
    public string Identifier { get; init; }
    public LoginSourceType LoginSource { get; init; }
}
