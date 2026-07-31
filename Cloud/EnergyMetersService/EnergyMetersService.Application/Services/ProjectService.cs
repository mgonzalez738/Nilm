using EnergyMetersService.Application.Constants;
using FluentValidation;
using Microsoft.Extensions.Logging;
using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Application.Interfaces;
using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Domain.Interfaces;
using EnergyMetersService.Application.Mappings;
using Mapster;

namespace EnergyMetersService.Application.Services;

/// <summary>
/// Provides application-level services for managing project entities.
/// Handles business logic, validation, security filtering, and data persistence for projects.
/// </summary>
public class ProjectService(
    IUserContext userContext,
    IProjectRepository projectRepository,
    //ISmartPlugSensorRepository smartPlugRepository,
    IValidator<ProjectCreateDto> createValidator,
    IValidator<ProjectUpdateDto> updateValidator,
    ILogger<ProjectService> logger) : IProjectService
{
    private readonly IUserContext _userContext = userContext;
    private readonly IProjectRepository _projectRepository = projectRepository;
    //private readonly ISmartPlugSensorRepository _smartPlugRepository = smartPlugRepository;
    private readonly IValidator<ProjectCreateDto> _createValidator = createValidator;
    private readonly IValidator<ProjectUpdateDto> _updateValidator = updateValidator;
    private readonly ILogger<ProjectService> _logger = logger;

    /// <summary>
    /// Retrieves a project by its unique identifier.
    /// Evaluates the user's roles to ensure adequate access rights before returning the data.
    /// </summary>
    public async Task<ProjectDto?> GetByIdAsync(string id)
    {
        var project = await _projectRepository.GetByIdAsync(id);

        if (project == null)
            return null;

        bool hasAccess = false;

        if (_userContext.IsSystem || _userContext.Roles.Contains(AppRoles.Super))
            hasAccess = true;
        else if (_userContext.Roles.Contains(AppRoles.Admin))
            hasAccess = project.CompanyId == _userContext.CompanyId;
        else if (_userContext.Roles.Contains(AppRoles.User))
            hasAccess = _userContext.ProjectIds != null && _userContext.ProjectIds.Contains(project.Id);

        if (!hasAccess)
            return null;

        return project.Adapt<ProjectDto>();
    }

    /// <summary>
    /// Builds a queryable collection of projects.
    /// Automatically applies security filters based on the user's roles and assigned project context.
    /// </summary>
    public IQueryable<ProjectDto> GetQueryable()
    {
        var query = _projectRepository.AsQueryable();

        if (_userContext.IsSystem || _userContext.Roles.Contains(AppRoles.Super))
            return query.ProjectToType<ProjectDto>();

        if (_userContext.Roles.Contains(AppRoles.Admin))
        {
            if (string.IsNullOrEmpty(_userContext.CompanyId))
                return Enumerable.Empty<ProjectDto>().AsQueryable();

            query = query.Where(project => project.CompanyId == _userContext.CompanyId);

            return query.ProjectToType<ProjectDto>();
        }

        if (_userContext.Roles.Contains(AppRoles.User))
        {
            var allowedProjectIds = _userContext.ProjectIds?.ToList() ?? [];

            if (allowedProjectIds.Count == 0)
                return Enumerable.Empty<ProjectDto>().AsQueryable();

            query = query.Where(project => allowedProjectIds.Contains(project.Id));

            return query.ProjectToType<ProjectDto>();
        }

        return Enumerable.Empty<ProjectDto>().AsQueryable();
    }

    /// <summary>
    /// Creates a new project record in the system.
    /// </summary>
    public async Task<string> CreateAsync(ProjectCreateDto dto)
    {
        bool isAuthorized = false;

        if (_userContext.IsSystem || _userContext.Roles.Contains(AppRoles.Super))
            isAuthorized = true;
        else if (_userContext.Roles.Contains(AppRoles.Admin))
        {
            if (dto.CompanyId == _userContext.CompanyId)
                isAuthorized = true;
        }

        if (!isAuthorized)
            throw new UnauthorizedAccessException("You do not have the required privileges to create a project for this company.");

        var validationResult = await _createValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        var project = dto.Adapt<Project>();

        await _projectRepository.AddAsync(project);

        return project.Id;
    }

    /// <summary>
    /// Updates an existing company's information.
    /// </summary>
    public async Task UpdateAsync(string id, ProjectUpdateDto dto)
    {
        var project = await _projectRepository.GetByIdAsync(id);

        if (project == null)
            throw new KeyNotFoundException($"Project with ID {id} was not found.");

        bool isAuthorized = false;

        if (_userContext.IsSystem || _userContext.Roles.Contains(AppRoles.Super))
            isAuthorized = true;
        else if (_userContext.Roles.Contains(AppRoles.Admin))
        {
            if (project.CompanyId == _userContext.CompanyId)
                isAuthorized = true;
        }

        if (!isAuthorized)
            throw new UnauthorizedAccessException("You do not have the required privileges to update this project.");

        var context = new ValidationContext<ProjectUpdateDto>(dto);
        context.RootContextData["ProjectId"] = project.Id;
        context.RootContextData["CompanyId"] = project.CompanyId;

        var validationResult = await _updateValidator.ValidateAsync(context);
        if (!validationResult.IsValid)
            throw new ValidationException(validationResult.Errors);

        dto.Adapt(project);

        await _projectRepository.UpdateAsync(project);
    }

    /// <summary>
    /// Deletes a project and removes the project ID from all associated sensors.
    /// </summary>
    public async Task DeleteAsync(string id)
    {
        var project = await _projectRepository.GetByIdAsync(id) 
                      ?? throw new KeyNotFoundException($"Project with ID {id} was not found.");

        bool isAuthorized = false;

        if (_userContext.IsSystem || _userContext.Roles.Contains(AppRoles.Super))
            isAuthorized = true;
        else if (_userContext.Roles.Contains(AppRoles.Admin))
        {
            if (project.CompanyId == _userContext.CompanyId)
                isAuthorized = true;
        }

        if (!isAuthorized)
            throw new UnauthorizedAccessException("You do not have the required privileges to delete this project.");

        /*var associatedSensors = _smartPlugRepository.AsQueryable()
                                                    .Where(s => s.ProjectIds.Contains(id))
                                                    .ToList();

        foreach (var sensor in associatedSensors)
        {
            sensor.RemoveProject(id);
            await _smartPlugRepository.UpdateAsync(sensor);
        }*/

        await _projectRepository.DeleteAsync(id);
    }
}