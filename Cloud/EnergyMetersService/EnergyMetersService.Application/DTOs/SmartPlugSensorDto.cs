using EnergyMetersService.Domain.ValueObjects;

namespace EnergyMetersService.Application.DTOs;

public record SmartPlugSensorDto
{
    public string Id { get; init; } = string.Empty;

    public string? Name { get; init; }

    public string? Description { get; init; }

    public string? CompanyId { get; set; }
    public CompanyDto? Company { get; set; }

    public IReadOnlyCollection<string>? ProjectIds { get; init; }

    public Location? Location { get; init; } 

    public SmartPlugSettings? Settings { get; init; }

    public Status? Status { get; init; }

    public string? GrafanaDashboardLink { get; init; }
}
