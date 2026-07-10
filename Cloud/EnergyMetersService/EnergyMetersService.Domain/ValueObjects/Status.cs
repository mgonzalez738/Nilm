namespace EnergyMetersService.Domain.ValueObjects;

public record Status()
{
    public const string StateUnknown = "Unknown";
    public const string StateWatchdog = "Watchdog";
    public const string StateNull = "Null";
    public const string StateLimit = "Limit";
    public const string StateNormal = "Normal";

    public DateTime? TimestampLastData = null;
    public Alerts Alerts = new();

    public string Info
    {
        get
        {
            if (TimestampLastData == null) return StateUnknown;
            if (Alerts.Watchdogs.Count != 0) return StateWatchdog;
            if (Alerts.Nulls.Count != 0) return StateNull;
            if (Alerts.Limits.Count != 0) return StateLimit;
            return StateNormal;
        }
    }
}