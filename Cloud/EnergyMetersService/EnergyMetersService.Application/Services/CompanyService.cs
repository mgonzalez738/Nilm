using EnergyMetersService.Application.Constants;
using FluentValidation;
using Microsoft.Extensions.Logging;
using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Application.Interfaces;
using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Domain.Interfaces;

namespace EnergyMetersService.Application.Services;

/// <summary>
/// Provides application-level services for managing company entities.
/// Handles business logic, validation, security filtering, and data persistence for companies.
/// </summary>
/// <param name="userContext"></param>
/// <param name="companyRepository"></param>
/// <param name="sensorRepository"></param>
/// <param name="createValidator"></param>
/// <param name="updateValidator"></param>
/// <param name="logger"></param>

public partial class CompanyService(
    IUserContext userContext,
    IEntityRepository<Company> companyRepository,
    IEntityRepository<SmartPlugSensor> sensorRepository,
    IValidator<CompanyCreateDto> createValidator,
    IValidator<CompanyUpdateDto> updateValidator,
    ILogger<CompanyService> logger) : ICompanyService
{
    private readonly IUserContext _userContext = userContext;
    private readonly IEntityRepository<Company> _companyRepository = companyRepository;
    private readonly IEntityRepository<SmartPlugSensor> _sensorRepository = sensorRepository;
    private readonly IValidator<CompanyCreateDto> _createValidator = createValidator;
    private readonly IValidator<CompanyUpdateDto> _updateValidator = updateValidator;
    private readonly ILogger<CompanyService> _logger = logger;

    /// <summary>
    /// Retrieves a company by its unique identifier.
    /// Evaluates the user's role to ensure adequate access rights before returning the data.
    /// </summary>
    /// <param name="id">The unique identifier of the company to retrieve.</param>
    /// <returns>A data transfer object representing the requested company.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when the company is not found or the user does not have permission to view it.</exception>
    public async Task<CompanyDto> GetByIdAsync(string id)
    {
        LogFetchingCompany(id);

        var company = await _companyRepository.GetByIdAsync(id);

        if (company == null ||
           (_userContext.Role != AppRoles.Super && !_userContext.IsSystem && _userContext.CompanyId != company.Id))
        {
            LogCompanyGetFailed(id);
            throw new KeyNotFoundException($"Company with ID {id} was not found.");
        }

        LogCompanyRetrieved(id);

        return new CompanyDto
        {
            Id = company.Id,
            Name = company.Name
        };
    }

    /// <summary>
    /// Builds a queryable collection of companies.
    /// Automatically applies security filters based on the user's role and assigned company context.
    /// Fails silently by returning an empty queryable if a standard user lacks a valid company identifier.
    /// </summary>
    /// <returns>An IQueryable of company data transfer objects.</returns>
    public IQueryable<CompanyDto> GetQueryable()
    {
        LogBuildingQueryable(_userContext.Role);

        var query = _companyRepository.AsQueryable();
        
        if (_userContext.Role != AppRoles.Super && !_userContext.IsSystem)
        {
            if (string.IsNullOrEmpty(_userContext.CompanyId))
            {
                LogMissingCompanyId(_userContext.Role);
                return Array.Empty<CompanyDto>().AsQueryable();
            }

            LogApplyingSecurityFilter(_userContext.CompanyId);
            query = query.Where(company => company.Id == _userContext.CompanyId);
        }
        
        return query.Select(company => new CompanyDto
        {
            Id = company.Id,
            Name = company.Name
        });
    }

    /// <summary>
    /// Creates a new company record in the system.
    /// Requires elevated privileges and performs strict validation on the incoming payload.
    /// </summary>
    /// <param name="dto">The data transfer object containing the details for the new company.</param>
    /// <returns>The unique identifier of the newly created company.</returns>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user lacks Super or System privileges.</exception>
    /// <exception cref="ValidationException">Thrown when the provided data fails business validation rules.</exception>
    public async Task<string> CreateCompanyAsync(CompanyCreateDto dto)
    {
        LogStartingCompanyCreation(dto.Name);
        
        if (_userContext.Role != AppRoles.Super && !_userContext.IsSystem)
        {
            LogUnauthorizedCreateAttempt();
            throw new UnauthorizedAccessException("Super or System privileges are required for this operation.");
        }

        var validationResult = await _createValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            LogCreateValidationFailed(dto.Name, validationResult.Errors);
            throw new ValidationException(validationResult.Errors);
        }

        var company = new Company
        {
            Name = dto.Name
        };

        await _companyRepository.AddAsync(company);

        LogCompanyCreated(company.Id);

        return company.Id;
    }

    /// <summary>
    /// Updates an existing company's information.
    /// Requires elevated privileges and validates the changes before applying them to the repository.
    /// </summary>
    /// <param name="id">The unique identifier of the company to update.</param>
    /// <param name="dto">The data transfer object containing the updated information.</param>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user lacks Super or System privileges.</exception>
    /// <exception cref="ValidationException">Thrown when the provided data fails business validation rules.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when the targeted company does not exist.</exception>
    public async Task UpdateCompanyAsync(string id, CompanyUpdateDto dto)
    {
        LogStartingCompanyUpdate(id);

        if (_userContext.Role != AppRoles.Super && !_userContext.IsSystem)
        {
            LogUnauthorizedUpdateAttempt(id);
            throw new UnauthorizedAccessException($"{AppRoles.Super} or System privileges are required for this operation.");
        }

        var context = new ValidationContext<CompanyUpdateDto>(dto);
        context.RootContextData["CompanyId"] = id;

        var validationResult = await _updateValidator.ValidateAsync(context);
        if (!validationResult.IsValid)
        {
            LogUpdateValidationFailed(id, validationResult.Errors);
            throw new ValidationException(validationResult.Errors);
        }

        var company = await _companyRepository.GetByIdAsync(id);

        if (company == null)
        {
            LogCompanyUpdateFailedNotFound(id);
            throw new KeyNotFoundException($"Company with ID {id} was not found.");
        }

        company.Name = dto.Name;

        await _companyRepository.UpdateAsync(company);

        LogCompanyUpdated(id);
    }

    /// <summary>
    /// Deletes a company and cascades the deletion to all associated sensors.
    /// Requires elevated privileges to execute.
    /// </summary>
    /// <param name="id">The unique identifier of the company to delete.</param>
    /// <exception cref="UnauthorizedAccessException">Thrown when the user lacks Super or System privileges.</exception>
    public async Task DeleteCompanyAsync(string id)
    {
        LogStartingCompanyDeletion(id);

        if (_userContext.Role != AppRoles.Super && !_userContext.IsSystem)
        {
            LogUnauthorizedDeleteAttempt(id);
            throw new UnauthorizedAccessException($"{AppRoles.Super} or System privileges are required for this operation.");
        }

        LogDeletingAssociatedSensors(id);
        await _sensorRepository.DeleteManyAsync(s => s.CompanyId == id);

        await _companyRepository.DeleteAsync(id);

        LogCompanyDeleted(id);
    }

    // Partial Log methods

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetching company with ID: {CompanyId}")]
    private partial void LogFetchingCompany(string companyId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to get company. Company with ID {CompanyId} was not found or permissions are insufficient.")]
    private partial void LogCompanyGetFailed(string companyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Company {CompanyId} successfully retrieved.")]
    private partial void LogCompanyRetrieved(string companyId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Building queryable for companies. Current role: {UserRole}")]
    private partial void LogBuildingQueryable(string userRole);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Applying security filter for company ID: {CompanyId}")]
    private partial void LogApplyingSecurityFilter(string companyId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting creation of new company with name: {CompanyName}")]
    private partial void LogStartingCompanyCreation(string companyName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unauthorized attempt to create a company by a user without Super/System privileges.")]
    private partial void LogUnauthorizedCreateAttempt();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Validation failed when attempting to create company {CompanyName}. Errors: {@ValidationErrors}")]
    private partial void LogCreateValidationFailed(string companyName, object validationErrors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Company successfully created with ID: {CompanyId}")]
    private partial void LogCompanyCreated(string companyId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting update for company with ID: {CompanyId}")]
    private partial void LogStartingCompanyUpdate(string companyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unauthorized attempt to update company {CompanyId}.")]
    private partial void LogUnauthorizedUpdateAttempt(string companyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Validation failed when updating company {CompanyId}. Errors: {@ValidationErrors}")]
    private partial void LogUpdateValidationFailed(string companyId, object validationErrors);

    [LoggerMessage(Level = LogLevel.Error, Message = "Update attempt failed. Company with ID {CompanyId} was not found.")]
    private partial void LogCompanyUpdateFailedNotFound(string companyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Company {CompanyId} successfully updated.")]
    private partial void LogCompanyUpdated(string companyId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting deletion of company with ID: {CompanyId}")]
    private partial void LogStartingCompanyDeletion(string companyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unauthorized attempt to delete company {CompanyId}.")]
    private partial void LogUnauthorizedDeleteAttempt(string companyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Deleting sensors associated with company {CompanyId}...")]
    private partial void LogDeletingAssociatedSensors(string companyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Company {CompanyId} and its associated sensors were successfully deleted.")]
    private partial void LogCompanyDeleted(string companyId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Queryable requested but user (Role: {UserRole}) has no CompanyId. Returning empty list.")]
    private partial void LogMissingCompanyId(string userRole);
}