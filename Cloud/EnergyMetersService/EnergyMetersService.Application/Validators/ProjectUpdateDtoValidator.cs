using FluentValidation;
using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Domain.Interfaces;

namespace EnergyMetersService.Application.Validators;

public class ProjectUpdateDtoValidator : AbstractValidator<ProjectUpdateDto>
{
    private readonly IProjectRepository _projectRepository;

    public ProjectUpdateDtoValidator(IProjectRepository projectRepository)
    {
        _projectRepository = projectRepository;

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre del proyecto es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede exceder los 100 caracteres.")
            .MustAsync(BeUniqueNameForOtherProjects).WithMessage("Ya existe otro proyecto con este nombre.");
    }

    private async Task<bool> BeUniqueNameForOtherProjects(ProjectUpdateDto dto, string? name, ValidationContext<ProjectUpdateDto> context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name)) return true;

        if (!context.RootContextData.TryGetValue("ProjectId", out var projectIdObj) || projectIdObj is not string projectId)
        {
            throw new InvalidOperationException("El ProjectId no fue inyectado en el contexto de validación.");
        }

        if (!context.RootContextData.TryGetValue("CompanyId", out var companyIdObj) || companyIdObj is not string companyId)
        {
            throw new InvalidOperationException("El CompanyId no fue inyectado en el contexto de validación.");
        }

        var exists = await _projectRepository.ExistsByNameAsync(name, companyId, projectId);

        return !exists;
    }
}
