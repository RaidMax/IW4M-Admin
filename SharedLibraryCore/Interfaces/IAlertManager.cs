using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using SharedLibraryCore.Alerts;
using SharedLibraryCore.Database.Models;

namespace SharedLibraryCore.Interfaces;

public interface IAlertManager
{
    /// <summary>
    /// Initializes the manager
    /// </summary>
    /// <returns></returns>
    Task Initialize();
    
    /// <summary>
    /// Get all the alerts for given client
    /// </summary>
    /// <param name="client">client to retrieve alerts for</param>
    /// <returns></returns>
    IEnumerable<Alert.AlertState> RetrieveAlerts(EFClient client);
    
    /// <summary>
    /// Trigger a new alert
    /// </summary>
    /// <param name="alert">Alert to trigger</param>
    void AddAlert(Alert.AlertState alert);
    
    /// <summary>
    /// Marks an alert as read and removes it from the manager
    /// </summary>
    /// <param name="alertId">Id of the alert to mark as read</param>
    void MarkAlertAsRead(Guid alertId);
    
    /// <summary>
    /// Mark all alerts intended for the given recipientId as read
    /// </summary>
    /// <param name="recipientId">Identifier of the recipient</param>
    void MarkAllAlertsAsRead(int recipientId);
    
    /// <summary>
    /// Registers a static (persistent) event source eg datastore that
    /// gets initialized at startup
    /// </summary>
    /// <param name="alertSource">Source action</param>
    void RegisterStaticAlertSource(Func<Task<IEnumerable<Alert.AlertState>>> alertSource);
    
    /// <summary>
    /// Fires when an alert has been consumed (dimissed)
    /// </summary>
    EventHandler<Alert.AlertState> OnAlertConsumed { get; set; }
    
}
