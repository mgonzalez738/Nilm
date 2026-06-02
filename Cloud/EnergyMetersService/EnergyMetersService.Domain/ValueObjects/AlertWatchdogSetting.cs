namespace EnergyMetersService.Domain.ValueObjects
{
    public record AlertWatchdogSettings(
        bool Enabled,
        int TimeoutLimitMinutes);
}