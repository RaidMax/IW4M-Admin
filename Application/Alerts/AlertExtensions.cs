using System;
using SharedLibraryCore;
using SharedLibraryCore.Alerts;
using SharedLibraryCore.Database.Models;

namespace IW4MAdmin.Application.Alerts;

public static class AlertExtensions
{
    public static Alert.AlertState BuildAlert(this EFClient client, Alert.AlertCategory? type = null)
    {
        return new Alert.AlertState
        {
            RecipientId = client.ClientId,
            Category = type ?? Alert.AlertCategory.Information
        };
    }

    public static Alert.AlertState WithCategory(this Alert.AlertState state, Alert.AlertCategory category)
    {
        state.Category = category;
        return state;
    }

    public static Alert.AlertState OfType(this Alert.AlertState state, string type)
    {
        state.Type = type;
        return state;
    }

    public static Alert.AlertState WithMessage(this Alert.AlertState state, string message)
    {
        state.Message = message;
        return state;
    }

    public static Alert.AlertState ExpiresIn(this Alert.AlertState state, TimeSpan expiration)
    {
        state.ExpiresAt = DateTime.Now.Add(expiration);
        return state;
    }
    
    public static Alert.AlertState FromSource(this Alert.AlertState state, string source)
    {
        state.Source = source;
        return state;
    }

    public static Alert.AlertState FromClient(this Alert.AlertState state, EFClient client)
    {
        state.Source = client.Name.StripColors();
        state.SourceId = client.ClientId;
        return state;
    }
}
