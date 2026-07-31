using EnergyMetersService.Application.DTOs;

namespace EnergyMetersService.Application.Interfaces
{
    public interface IProjectService
    {
        Task<ProjectDto?> GetByIdAsync(string id);
        IQueryable<ProjectDto> GetQueryable();
        Task<string> CreateAsync(ProjectCreateDto dto);       
        Task UpdateAsync(string id, ProjectUpdateDto dto);
        Task DeleteAsync(string id);
    }
}