namespace EnergyMetersService.Domain.ValueObjects;

public record AlertAnalogSettings(
    bool NullEnabled = false,
    bool LimitEnabled = false,
    double LimitLow = 0,
    double LimitHigh = 0,
    double LimitHysteresis = 0 );