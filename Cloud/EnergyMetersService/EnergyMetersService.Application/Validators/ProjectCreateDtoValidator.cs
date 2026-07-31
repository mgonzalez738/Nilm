using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Domain.Interfaces;
using FluentValidation;
using System.Text.RegularExpressions;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace EnergyMetersService.Application.Validators;

public class ProjectCreateDtoValidator : AbstractValidator<ProjectCreateDto>
{
    private readonly IProjectRepository _projectRepository;
    private readonly ICompanyRepository _companyRepository;

    public ProjectCreateDtoValidator(IProjectRepository projectRepository, ICompanyRepository companyRepository)
    {
        _projectRepository = projectRepository;
        _companyRepository = companyRepository;

        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El nombre del proyecto es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder los 100 caracteres.")
            .MustAsync(BeUniqueName).WithMessage("Ya existe un proyecto con este nombre.");

        RuleFor(x => x.CompanyId)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("El ID de la compañía es obligatorio.")
            .Must(BeAValidObjectId).WithMessage("El formato del ID de la compañía no es válido.")
            .MustAsync(CompanyExistsAsync).WithMessage("La compañía especificada no existe en la base de datos.");
    }

    private async Task<bool> BeUniqueName(ProjectCreateDto model, string? name, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;

        if (!BeAValidObjectId(model.CompanyId)) return true;

        var exists = await _projectRepository.ExistsByNameAsync(name, model.CompanyId);
        return !exists;
    }

    private bool BeAValidObjectId(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length != 24)
            return false;

        return Regex.IsMatch(id, @"^[0-9a-fA-F]{24}$");
    }

    private async Task<bool> CompanyExistsAsync(string companyId, CancellationToken cancellationToken)
    {
        return await _companyRepository.ExistsAsync(c => c.Id == companyId);
    }
}
