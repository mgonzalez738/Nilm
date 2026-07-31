using EnergyMetersService.Domain.Entities;

namespace EnergyMetersService.Domain.Interfaces;

public interface IProjectRepository : IEntityRepository<Project>
{
    Task<bool> ExistsByNameAsync(string name, string? companyId = null, string? ignoreId = null);
}