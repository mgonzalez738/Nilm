using SmartPlugService.Domain.ValueObjects;

namespace SmartPlugService.Domain.Entities;

public class Sensor : Entity
{
    public const string VoltageUnit = "V";
    public const string CurrentUnit = "A";
    public const string ActivePowerUnit = "W";
    public const string ReactivePowerUnit = "VAr";

    private readonly List<string> _projectIds = [];

    public string Name { get; private set; }
    public string Description { get; private set; }
    public string CompanyId { get; private set; }
    public IReadOnlyCollection<string> ProjectIds => _projectIds.AsReadOnly();
    public Location Location { get; private set; }
    public Settings Settings { get; private set; }
    public Status Status { get; private set; }
    public string? GrafanaDashboardLink { get; private set; }

    public Sensor(string name, string companyId, Settings defaultSettings)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required");
        if (string.IsNullOrWhiteSpace(companyId)) throw new ArgumentException("CompanyId is required");

        Name = name;
        Description = string.Empty;
        CompanyId = companyId;
        Location = new Location(false, 0, 0);
        Settings = defaultSettings;
        Status = new Status(null, Alerts.Empty);
    }

    public void UpdateLocation(bool enabled, double? latitude = null, double? longitude = null)
    {
        if(enabled)
        {
            if (latitude == null || longitude == null)
                throw new ArgumentException("Latitude and Longitude must be provided when location is enabled.");

            Location = new Location(true, latitude.Value, longitude.Value);
        }
        else
        {
            Location = new Location(false, 0, 0);
        }
    }

    public void UpdateSettings(Settings newSettings)
    {
        Settings = newSettings;
    }

    public void RegisterLimitAlert(AlertLimit newAlert)
    {
        if (string.IsNullOrWhiteSpace(newAlert.Property))
            throw new ArgumentException("Alert property cannot be null or empty.");

        if (Status.Alerts.Limits.Any(a => a.Property == newAlert.Property))
            throw new InvalidOperationException($"Alert limit for property {newAlert.Property} already exists.");

        var updatedLimits = new List<AlertLimit>(Status.Alerts.Limits) { newAlert };
        var newAlerts = Status.Alerts with { Limits = updatedLimits };

        Status = Status with { Alerts = newAlerts };
    }

    public void ClearAlert(string propertyName)
    {
        var updatedLimits = Status.Alerts.Limits.Where(a => a.Property != propertyName).ToList();
        var updatedWatchdogs = Status.Alerts.Watchdogs.Where(a => a.Property != propertyName).ToList();

        var newAlerts = new Alerts(updatedWatchdogs, updatedLimits, Status.Alerts.Nulls);

        Status = Status with { Alerts = newAlerts };
    }

    public void UpdateLastTelemetry(DateTime timestamp)
    {
        Status = Status with { TimestampLastData = timestamp };
    }
}