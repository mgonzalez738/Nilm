using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Domain.Interfaces;
using FluentValidation;

namespace EnergyMetersService.Application.Validators;

public class SmartPlugSensorCreateDtoValidator : AbstractValidator<SmartPlugSensorCreateDto>
{
    private readonly IEntityRepository<SmartPlugSensor> _sensorRepository;
    private readonly IEntityRepository<Company> _companyRepository;

    public SmartPlugSensorCreateDtoValidator(
        IEntityRepository<SmartPlugSensor> sensorRepository,
        IEntityRepository<Company> companyRepository)
    {
        _sensorRepository = sensorRepository;
        _companyRepository = companyRepository;

        RuleFor(x => x.CompanyId)
            .NotEmpty().WithMessage("El CompanyId es obligatorio.")
            .MustAsync(CompanyExistsAsync).WithMessage("La compañía especificada no existe.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del sensor es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder los 100 caracteres.")
            .MustAsync(BeUniqueNameWithinCompany).WithMessage("Ya existe un sensor con este nombre en la compañía especificada.");
    }

    private async Task<bool> CompanyExistsAsync(string companyId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(companyId)) return false;
        return await _companyRepository.ExistsAsync(c => c.Id == companyId);
    }

    private async Task<bool> BeUniqueNameWithinCompany(SmartPlugSensorCreateDto dto, string name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(dto.CompanyId)) return true;

        var exists = await _sensorRepository.ExistsAsync(s => s.CompanyId == dto.CompanyId && s.Name == name);
        return !exists;
    }
}

    