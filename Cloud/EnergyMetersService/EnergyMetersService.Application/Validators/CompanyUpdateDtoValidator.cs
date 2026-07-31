using FluentValidation;
using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Domain.Interfaces;

namespace EnergyMetersService.Application.Validators;

public class CompanyUpdateDtoValidator : AbstractValidator<CompanyUpdateDto>
{
    private readonly ICompanyRepository _companyRepository;

    public CompanyUpdateDtoValidator(ICompanyRepository companyRepository)
    {
        _companyRepository = companyRepository;

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre de la compañía es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder los 100 caracteres.")
            .MustAsync(BeUniqueNameForOtherCompanies).WithMessage("Ya existe otra compañía con este nombre.");
    }

    private async Task<bool> BeUniqueNameForOtherCompanies(CompanyUpdateDto dto, string? name, ValidationContext<CompanyUpdateDto> context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;

        if (!context.RootContextData.TryGetValue("CompanyId", out var companyIdObj) || companyIdObj is not string companyId)
        {
            throw new InvalidOperationException("El CompanyId no fue inyectado en el contexto de validación.");
        }

        var exists = await _companyRepository.ExistsByNameAsync(name, companyId);

        return !exists;
    }
}
