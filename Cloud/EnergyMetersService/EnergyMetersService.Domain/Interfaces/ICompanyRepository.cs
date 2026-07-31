using EnergyMetersService.Domain.Entities;

namespace EnergyMetersService.Domain.Interfaces;

public interface ICompanyRepository : IEntityRepository<Company>
{
    Task<bool> ExistsByNameAsync(string name, string? ignoreId = null);
}