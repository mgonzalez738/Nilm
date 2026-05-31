using FluentValidation;
using SmartPlugService.Application.DTOs;
using SmartPlugService.Domain.Entities;
using SmartPlugService.Domain.Interfaces;

namespace SmartPlugService.Application.Validators;

public class CompanyUpdateDtoValidator : AbstractValidator<CompanyUpdateDto>
{
    private readonly IEntityRepository<Company> _companyRepository;

    public CompanyUpdateDtoValidator(IEntityRepository<Company> companyRepository)
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

        var exists = await _companyRepository.ExistsAsync(c => c.Name == name && c.Id != companyId);

        return !exists;
    }
}
