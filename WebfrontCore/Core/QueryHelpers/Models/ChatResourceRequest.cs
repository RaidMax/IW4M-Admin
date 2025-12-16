using Stats.Dtos;

namespace WebfrontCore.QueryHelpers.Models;

public class ChatResourceRequest : ChatSearchQuery
{
    public bool HasData => !string.IsNullOrEmpty(MessageContains) || !string.IsNullOrEmpty(ServerId) ||
                           ClientId is not null || SentAfterDateTime is not null;
}
