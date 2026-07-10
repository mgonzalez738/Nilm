namespace EnergyMetersService.Domain.ValueObjects
{
    public record AlertWatchdogSettings(
        bool Enabled = false,
        int TimeoutLimitMinutes = 0);
}