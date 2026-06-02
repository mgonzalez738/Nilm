namespace EnergyMetersService.Domain.ValueObjects;

public record SmartPlugAlertSettings (
    AlertWatchdogSettings Watchdog,
    AlertAnalogSettings Voltage,
    AlertAnalogSettings Current,
    AlertAnalogSettings ActivePower,
    AlertAnalogSettings ReactivePower);