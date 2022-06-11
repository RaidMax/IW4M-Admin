using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SharedLibraryCore.Alerts;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Interfaces;

namespace IW4MAdmin.Application.Alerts;

public class AlertManager : IAlertManager
{
    private readonly ApplicationConfiguration _appConfig;
    private readonly ConcurrentDictionary<int, List<Alert.AlertState>> _states = new();
    private readonly List<Func<Task<IEnumerable<Alert.AlertState>>>> _staticSources = new();

    public AlertManager(ApplicationConfiguration appConfig)
    {
        _appConfig = appConfig;
        _states.TryAdd(0, new List<Alert.AlertState>());
    }

    public EventHandler<Alert.AlertState> OnAlertConsumed { get; set; }

    public async Task Initialize()
    {
        foreach (var source in _staticSources)
        {
            var alerts = await source();
            foreach (var alert in alerts)
            {
                AddAlert(alert);
            }
        }
    }

    public IEnumerable<Alert.AlertState> RetrieveAlerts(EFClient client)
    {
        lock (_states)
        {
            var alerts = Enumerable.Empty<Alert.AlertState>();
            if (client.Level > Data.Models.Client.EFClient.Permission.Trusted)
            {
                alerts = alerts.Concat(_states[0].Where(alert =>
                    alert.MinimumPermission is null || alert.MinimumPermission <= client.Level));
            }

            if (_states.ContainsKey(client.ClientId))
            {
                alerts = alerts.Concat(_states[client.ClientId].AsReadOnly());
            }

            return alerts.OrderByDescending(alert => alert.OccuredAt);
        }
    }

    public void MarkAlertAsRead(Guid alertId)
    {
        lock (_states)
        {
            foreach (var items in _states.Values)
            {
                var matchingEvent = items.FirstOrDefault(item => item.AlertId == alertId);

                if (matchingEvent is null)
                {
                    continue;
                }

                items.Remove(matchingEvent);
                OnAlertConsumed?.Invoke(this, matchingEvent);
            }
        }
    }

    public void MarkAllAlertsAsRead(int recipientId)
    {
        lock (_states)
        {
            foreach (var items in _states.Values)
            {
                items.RemoveAll(item =>
                {
                    if (item.RecipientId != null && item.RecipientId != recipientId)
                    {
                        return false;
                    }

                    OnAlertConsumed?.Invoke(this, item);
                    return true;
                });
            }
        }
    }

    public void AddAlert(Alert.AlertState alert)
    {
        lock (_states)
        {
            if (alert.RecipientId is null)
            {
                _states[0].Add(alert);
                return;
            }

            if (!_states.ContainsKey(alert.RecipientId.Value))
            {
                _states[alert.RecipientId.Value] = new List<Alert.AlertState>();
            }

            if (_appConfig.MinimumAlertPermissions.ContainsKey(alert.Type))
            {
                alert.MinimumPermission = _appConfig.MinimumAlertPermissions[alert.Type];
            }

            _states[alert.RecipientId.Value].Add(alert);

            PruneOldAlerts();
        }
    }

    public void RegisterStaticAlertSource(Func<Task<IEnumerable<Alert.AlertState>>> alertSource)
    {
        _staticSources.Add(alertSource);
    }


    private void PruneOldAlerts()
    {
        foreach (var value in _states.Values)
        {
            value.RemoveAll(item => item.ExpiresAt < DateTime.UtcNow);
        }
    }
}
