namespace EnergyMetersService.Application.DTOs;

public class  ProjectDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public CompanyDto? Company { get; set; } 
}