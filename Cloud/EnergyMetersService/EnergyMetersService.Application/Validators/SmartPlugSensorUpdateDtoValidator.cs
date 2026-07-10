using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Domain.Interfaces;
using FluentValidation;

namespace EnergyMetersService.Application.Validators;

public class SmartPlugSensorUpdateDtoValidator : AbstractValidator<SmartPlugSensorUpdateDto>
{
    private readonly IEntityRepository<SmartPlugSensor> _sensorRepository;

    public SmartPlugSensorUpdateDtoValidator(IEntityRepository<SmartPlugSensor> sensorRepository)
    {
        _sensorRepository = sensorRepository;

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del sensor es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder los 100 caracteres.")
            .MustAsync(BeUniqueNameForOtherSensorsInCompany).WithMessage("Ya existe otro sensor con este nombre en la compañía.");
    }

    private async Task<bool> BeUniqueNameForOtherSensorsInCompany(SmartPlugSensorUpdateDto dto, string name, ValidationContext<SmartPlugSensorUpdateDto> context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;

        if (!context.RootContextData.TryGetValue("SensorId", out var sensorIdObj) || sensorIdObj is not string sensorId)
        {
            throw new InvalidOperationException("El SensorId no fue inyectado en el contexto de validación.");
        }

        if (!context.RootContextData.TryGetValue("CompanyId", out var companyIdObj) || companyIdObj is not string companyId)
        {
            throw new InvalidOperationException("El CompanyId no fue inyectado en el contexto de validación.");
        }

        var exists = await _sensorRepository.ExistsAsync(s =>
            s.CompanyId == companyId &&
            s.Name == name &&
            s.Id != sensorId);

        return !exists;
    }
}
