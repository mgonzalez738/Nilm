using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Domain.Entities;
using Mapster;

namespace EnergyMetersService.Application.Mappings;

public class MappingConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<SmartPlugSensor, SmartPlugSensorDto>()
              .Ignore(dest => dest.Company!);
        config.NewConfig<Company, CompanyDto>();
    }
}
 