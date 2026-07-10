namespace EnergyMetersService.Application.DTOs;

public record SmartPlugSensorCreateDto
{
    public string Name { get; init; } = string.Empty;

    public string CompanyId { get; init; } = string.Empty;
}