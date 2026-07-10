namespace EnergyMetersService.Domain.Entities;

public class Project : Entity
{
    public required string Name { get; set; } 

    public required string CompanyId { get; set; }

    public Company? Company { get; set; }
}