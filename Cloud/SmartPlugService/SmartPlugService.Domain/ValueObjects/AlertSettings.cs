namespace SmartPlugService.Domain.ValueObjects;

public record AlertSettings (
    AlertWatchdogSettings Watchdog,
    AlertAnalogSettings Voltage,
    AlertAnalogSettings Current,
    AlertAnalogSettings ActivePower,
    AlertAnalogSettings ReactivePower);