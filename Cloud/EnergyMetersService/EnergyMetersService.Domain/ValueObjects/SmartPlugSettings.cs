namespace EnergyMetersService.Domain.ValueObjects;

public record SmartPlugSettings()
{
    public SmartPlugAlertSettings Alerts { get; init; } = new();
}