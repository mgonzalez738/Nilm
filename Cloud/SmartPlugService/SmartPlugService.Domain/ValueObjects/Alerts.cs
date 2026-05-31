namespace SmartPlugService.Domain.ValueObjects;

public record Alerts(
    IReadOnlyCollection<AlertWatchdog> Watchdogs,
    IReadOnlyCollection<AlertLimit> Limits,
    IReadOnlyCollection<AlertNull> Nulls)
{
    public static Alerts Empty => new([], [], []);
}