using EnergyMetersService.Application.DTOs;

namespace EnergyMetersService.Application.Interfaces
{
    public interface ISmartPlugSensorService
    {
        Task<string> CreateSensorAsync(SmartPlugSensorCreateDto dto);
        Task DeleteSensorAsync(string id);
        Task<SmartPlugSensorDto> GetByIdAsync(string id);
        IQueryable<SmartPlugSensorDto> GetQueryable();
        Task UpdateSensorAsync(string id, SmartPlugSensorUpdateDto dto);
        void EnrichExpands(SmartPlugSensorDto sensor, IEnumerable<string> expands);
        void EnrichExpands(List<SmartPlugSensorDto> sensors, IEnumerable<string> expands);
    }
}