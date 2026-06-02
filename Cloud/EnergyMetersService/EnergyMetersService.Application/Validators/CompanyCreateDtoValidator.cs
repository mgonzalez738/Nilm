using FluentValidation;
using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Domain.Interfaces;

namespace EnergyMetersService.Application.Validators;

public class CompanyCreateDtoValidator : AbstractValidator<CompanyCreateDto>
{
    private readonly IEntityRepository<Company> _companyRepository;

    public CompanyCreateDtoValidator(IEntityRepository<Company> companyRepository)
    {
        _companyRepository = companyRepository;

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la compañía es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder los 100 caracteres.")
            .MustAsync(BeUniqueName).WithMessage("Ya existe una compañía con este nombre.");
    }

    private async Task<bool> BeUniqueName(string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;

        var exists = await _companyRepository.ExistsAsync(c => c.Name == name);
        return !exists;
    }
}
