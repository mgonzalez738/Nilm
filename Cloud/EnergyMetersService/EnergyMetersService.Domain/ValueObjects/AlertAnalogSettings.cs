namespace EnergyMetersService.Domain.ValueObjects;

public record AlertAnalogSettings(
    bool NullEnabled,
    bool LimitEnabled,
    double LimitLow,
    double LimitHigh,
    double LimitHysteresis);