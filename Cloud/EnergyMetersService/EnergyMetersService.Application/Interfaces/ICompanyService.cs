using EnergyMetersService.Application.DTOs;

namespace EnergyMetersService.Application.Interfaces
{
    public interface ICompanyService
    {
        Task<CompanyDto> GetByIdAsync(string id);
        IQueryable<CompanyDto> GetQueryable();
        Task<string> CreateCompanyAsync(CompanyCreateDto dto);       
        Task UpdateCompanyAsync(string id, CompanyUpdateDto dto);
        Task DeleteCompanyAsync(string id);
    }
}