namespace EnergyMetersService.Domain.ValueObjects;

public record SmartPlugAlertSettings
{
    public AlertWatchdogSettings Watchdog { get; init; } = new();

    public AlertAnalogSettings Voltage { get; init; } = new();

    public AlertAnalogSettings Current { get; init; } = new();

    public AlertAnalogSettings ActivePower { get; init; } = new();

    public AlertAnalogSettings ReactivePower { get; init; } = new();
}