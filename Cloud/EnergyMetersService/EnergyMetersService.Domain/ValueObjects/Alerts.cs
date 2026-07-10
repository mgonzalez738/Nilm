namespace EnergyMetersService.Domain.ValueObjects;

public record Alerts()
{
    public IReadOnlyCollection<AlertWatchdog> Watchdogs { get; init; } = [];
    public IReadOnlyCollection<AlertLimit> Limits { get; init; } = [];
    public IReadOnlyCollection<AlertNull> Nulls { get; init; } = [];
}