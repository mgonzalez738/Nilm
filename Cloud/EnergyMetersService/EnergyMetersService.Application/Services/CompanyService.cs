using EnergyMetersService.Application.Constants;
using FluentValidation;
using Microsoft.Extensions.Logging;
using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Application.Interfaces;
using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Domain.Interfaces;
using System.Linq;
using Mapster;

namespace EnergyMetersService.Application.Services;

/// <summary>
/// Provides application-level services for managing company entities.
/// Handles business logic, validation, security filtering, and data persistence for companies.
/// </summary>
public class CompanyService(
    IUserContext userContext,
    ICompanyRepository companyRepository,
    //IEntityRepository<SmartPlugSensor> sensorRepository,
    IValidator<CompanyCreateDto> createValidator,
    IValidator<CompanyUpdateDto> updateValidator,
    ILogger<CompanyService> logger) : ICompanyService
{
    private readonly IUserContext _userContext = userContext;
    private readonly ICompanyRepository _companyRepository = companyRepository;
    //private readonly IEntityRepository<SmartPlugSensor> _sensorRepository = sensorRepository;
    private readonly IValidator<CompanyCreateDto> _createValidator = createValidator;
    private readonly IValidator<CompanyUpdateDto> _updateValidator = updateValidator;
    private readonly ILogger<CompanyService> _logger = logger;

    /// <summary>
    /// Retrieves a company by its unique identifier.
    /// Evaluates the user's roles to ensure adequate access rights before returning the data.
    /// </summary>
    public async Task<CompanyDto?> GetByIdAsync(string id)
    {
        var company = await _companyRepository.GetByIdAsync(id);

        if(!_userContext.Roles.Contains(AppRoles.Super) && !_userContext.IsSystem && _userContext.CompanyId != company?.Id)
            company = null;

        return company.Adapt<CompanyDto>();
    }

    /// <summary>
    /// Builds a queryable collection of companies.
    /// Automatically applies security filters based on the user's roles and assigned company context.
    /// </summary>
    public IQueryable<CompanyDto> GetQueryable()
    {
        var query = _companyRepository.AsQueryable();

        if (!_userContext.Roles.Contains(AppRoles.Super) && !_userContext.IsSystem)
        {
            if (string.IsNullOrEmpty(_userContext.CompanyId))
            {
                return Array.Empty<CompanyDto>().AsQueryable();
            }
            query = query.Where(company => company.Id == _userContext.CompanyId);
        }

        return query.ProjectToType<CompanyDto>();
    }

    /// <summary>
    /// Creates a new company record in the system.
    /// </summary>
    public async Task<string> CreateCompanyAsync(CompanyCreateDto dto)
    {
        if (!_userContext.Roles.Contains(AppRoles.Super) && !_userContext.IsSystem)
        {
            throw new UnauthorizedAccessException("Super or System privileges are required for this operation.");
        }

        var validationResult = await _createValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var company = dto.Adapt<Company>();
        
        await _companyRepository.AddAsync(company);

        return company.Id;
    }

    /// <summary>
    /// Updates an existing company's information.
    /// </summary>
    public async Task UpdateCompanyAsync(string id, CompanyUpdateDto dto)
    {
        if (!_userContext.Roles.Contains(AppRoles.Super) && !_userContext.IsSystem)
        {
            throw new UnauthorizedAccessException($"{AppRoles.Super} or System privileges are required for this operation.");
        }

        var context = new ValidationContext<CompanyUpdateDto>(dto);
        context.RootContextData["CompanyId"] = id;

        var validationResult = await _updateValidator.ValidateAsync(context);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.Errors);
        }

        var company = await _companyRepository.GetByIdAsync(id);

        if (company == null)
        {
            throw new KeyNotFoundException($"Company with ID {id} was not found.");
        }

        dto.Adapt(company);

        await _companyRepository.UpdateAsync(company);
    }

    /// <summary>
    /// Deletes a company and cascades the deletion to all associated sensors.
    /// </summary>
    public async Task DeleteCompanyAsync(string id)
    {
        if (!_userContext.Roles.Contains(AppRoles.Super) && !_userContext.IsSystem)
        {
            throw new UnauthorizedAccessException($"{AppRoles.Super} or System privileges are required for this operation.");
        }

        //await _sensorRepository.DeleteManyAsync(s => s.CompanyId == id);

        await _companyRepository.DeleteAsync(id);
    }
}