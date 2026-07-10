using EnergyMetersService.Application.Constants;
using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Application.Interfaces;
using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Domain.Interfaces;
using FluentValidation;
using Mapster;
using Microsoft.Extensions.Logging;

namespace EnergyMetersService.Application.Services;

public partial class SmartPlugSensorService(
    IUserContext userContext,
    IEntityRepository<SmartPlugSensor> sensorRepository,
    IEntityRepository<Company> companyRepository,
    IValidator<SmartPlugSensorCreateDto> createValidator,
    IValidator<SmartPlugSensorUpdateDto> updateValidator,
    ILogger<SmartPlugSensorService> logger) : ISmartPlugSensorService
{
    private readonly IUserContext _userContext = userContext;
    private readonly IEntityRepository<SmartPlugSensor> _sensorRepository = sensorRepository;
    private readonly IEntityRepository<Company> _companyRepository = companyRepository;
    private readonly IValidator<SmartPlugSensorCreateDto> _createValidator = createValidator;
    private readonly IValidator<SmartPlugSensorUpdateDto> _updateValidator = updateValidator;
    private readonly ILogger<SmartPlugSensorService> _logger = logger;

    private bool HasWritePrivileges => _userContext.Roles.Contains(AppRoles.Super) ||
                                       _userContext.Roles.Contains(AppRoles.Admin) ||
                                       _userContext.IsSystem;

    private bool HasFullReadPrivileges => _userContext.Roles.Contains(AppRoles.Super) || _userContext.IsSystem;

    public async Task<SmartPlugSensorDto> GetByIdAsync(string id)
    {
        LogFetchingSensor(id);

        var sensor = await _sensorRepository.GetByIdAsync(id);

        if (sensor == null)
        {
            LogSensorNotFound(id);
            throw new KeyNotFoundException($"Sensor with ID {id} was not found.");
        }

        if (!HasFullReadPrivileges)
        {
            bool sameCompany = _userContext.CompanyId == sensor.CompanyId;
            bool sharedProjects = _userContext.ProjectIds != null &&
                                  sensor.ProjectIds.Any(p => _userContext.ProjectIds.Contains(p));

            if (!sameCompany || !sharedProjects)
            {
                LogSensorAccessDenied(id, _userContext.CompanyId ?? "None");
                throw new KeyNotFoundException($"Sensor with ID {id} was not found.");
            }
        }

        LogSensorRetrieved(id);

        var dto = sensor.Adapt<SmartPlugSensorDto>();

        if (!string.IsNullOrEmpty(dto.CompanyId))
        {
            var companyEntity = await _companyRepository.GetByIdAsync(dto.CompanyId);

            if (companyEntity != null)
            {
                dto.Company = companyEntity.Adapt<CompanyDto>();
                dto.CompanyId = null;
            }
        }

        return dto;
    }

    public IQueryable<SmartPlugSensorDto> GetQueryable()
    {
        var userRolesStr = string.Join(", ", _userContext.Roles);
        LogBuildingQueryable(userRolesStr);

        var query = _sensorRepository.AsQueryable();

        if (!HasFullReadPrivileges)
        {
            if (string.IsNullOrEmpty(_userContext.CompanyId) || _userContext.ProjectIds == null || !_userContext.ProjectIds.Any())
            {
                LogMissingCompanyOrProjects(userRolesStr);
                return Array.Empty<SmartPlugSensorDto>().AsQueryable();
            }

            LogApplyingSecurityFilter(_userContext.CompanyId);

            // Filtro por compañía y por intersección de proyectos
            query = query.Where(sensor =>
                sensor.CompanyId == _userContext.CompanyId &&
                sensor.ProjectIds.Any(p => _userContext.ProjectIds.Contains(p))
            );
        }

        return query.ProjectToType<SmartPlugSensorDto>();
    }

    public async Task<string> CreateSensorAsync(SmartPlugSensorCreateDto dto)
    {
        LogStartingSensorCreation(dto.Name);

        if (!HasWritePrivileges)
        {
            LogUnauthorizedCreateAttempt();
            throw new UnauthorizedAccessException("Super, Admin, or System privileges are required for this operation.");
        }

        var validationResult = await _createValidator.ValidateAsync(dto);
        if (!validationResult.IsValid)
        {
            LogCreateValidationFailed(dto.Name, validationResult.Errors);
            throw new ValidationException(validationResult.Errors);
        }

        var sensor = new SmartPlugSensor(dto.Name, dto.CompanyId);

        await _sensorRepository.AddAsync(sensor);
        LogSensorCreated(sensor.Id);

        return sensor.Id;
    }

    public async Task UpdateSensorAsync(string id, SmartPlugSensorUpdateDto dto)
    {
        LogStartingSensorUpdate(id);

        if (!HasWritePrivileges)
        {
            LogUnauthorizedUpdateAttempt(id);
            throw new UnauthorizedAccessException("Super, Admin, or System privileges are required for this operation.");
        }

        var sensor = await _sensorRepository.GetByIdAsync(id);
        if (sensor == null)
        {
            LogSensorUpdateFailedNotFound(id);
            throw new KeyNotFoundException($"Sensor with ID {id} was not found.");
        }

        var context = new ValidationContext<SmartPlugSensorUpdateDto>(dto);
        context.RootContextData["SensorId"] = id;
        context.RootContextData["CompanyId"] = sensor.CompanyId; // Extraído para validador

        var validationResult = await _updateValidator.ValidateAsync(context);
        if (!validationResult.IsValid)
        {
            LogUpdateValidationFailed(id, validationResult.Errors);
            throw new ValidationException(validationResult.Errors);
        }

        // Actualización de propiedades mediante los métodos de dominio
        //if (dto.Settings != null) sensor.UpdateSettings(dto.Settings);
        // Asumiendo que el DTO trae info de locación:
        // sensor.UpdateLocation(dto.EnableLocation, dto.Latitude, dto.Longitude);

        await _sensorRepository.UpdateAsync(sensor);
        LogSensorUpdated(id);
    }

    public async Task DeleteSensorAsync(string id)
    {
        LogStartingSensorDeletion(id);

        if (!HasWritePrivileges)
        {
            LogUnauthorizedDeleteAttempt(id);
            throw new UnauthorizedAccessException("Super, Admin, or System privileges are required for this operation.");
        }

        await _sensorRepository.DeleteAsync(id);
        LogSensorDeleted(id);
    }

    public void EnrichExpands(SmartPlugSensorDto sensor, IEnumerable<string> expands)
    {
        if (sensor == null || expands == null) return;

        var hasCompanyExpand = expands.Contains("Company", StringComparer.OrdinalIgnoreCase);

        if (hasCompanyExpand && !string.IsNullOrEmpty(sensor.CompanyId))
        {
            // 1. Buscamos la entidad en MongoDB
            var companyEntity = _companyRepository.AsQueryable()
                                                  .FirstOrDefault(c => c.Id == sensor.CompanyId);

            if (companyEntity != null)
            {
                // 2. MAPSTER: Mapeo directo de objeto a objeto usando .Adapt<T>()
                sensor.Company = companyEntity.Adapt<CompanyDto>();
                sensor.CompanyId = null;
            }
        }
    }

    public void EnrichExpands(List<SmartPlugSensorDto> sensors, IEnumerable<string> expands)
    {
        if (sensors == null || !sensors.Any() || expands == null) return;

        var hasCompanyExpand = expands.Contains("Company", StringComparer.OrdinalIgnoreCase);

        if (hasCompanyExpand)
        {
            var companyIds = sensors.Select(s => s.CompanyId)
                                    .Where(id => !string.IsNullOrEmpty(id))
                                    .Distinct()
                                    .ToList();

            // 1. MAPSTER + IQueryable: Proyectamos eficientemente en la BD antes del .ToDictionary()
            // Mongo solo traerá los campos necesarios para el CompanyDto
            var companyDict = _companyRepository.AsQueryable()
                                                .Where(c => companyIds.Contains(c.Id))
                                                .ProjectToType<CompanyDto>() // 👈 Magia de Mapster en IQueryable
                                                .ToDictionary(c => c.Id);

            // 2. Asignamos los DTOs correspondientes y limpiamos el ID
            foreach (var sensor in sensors)
            {
                if (sensor.CompanyId != null && companyDict.TryGetValue(sensor.CompanyId, out var companyDto))
                {
                    sensor.Company = companyDto;
                    sensor.CompanyId = null;
                }
            }
        }
    }

    // --- Partial Log methods ---

    [LoggerMessage(Level = LogLevel.Debug, Message = "Fetching sensor with ID: {SensorId}")]
    private partial void LogFetchingSensor(string sensorId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sensor with ID {SensorId} was not found.")]
    private partial void LogSensorNotFound(string sensorId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Access denied for sensor {SensorId}. User Company: {UserCompanyId}")]
    private partial void LogSensorAccessDenied(string sensorId, string userCompanyId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sensor {SensorId} successfully retrieved.")]
    private partial void LogSensorRetrieved(string sensorId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Building queryable for sensors. Current roles: {UserRoles}")]
    private partial void LogBuildingQueryable(string userRoles);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Queryable requested but user (Roles: {UserRoles}) lacks CompanyId or ProjectIds. Returning empty.")]
    private partial void LogMissingCompanyOrProjects(string userRoles);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Applying security filter for sensors. User Company ID: {CompanyId}")]
    private partial void LogApplyingSecurityFilter(string companyId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting creation of new sensor: {SensorName}")]
    private partial void LogStartingSensorCreation(string sensorName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unauthorized attempt to create a sensor. Lacks Super/Admin/System privileges.")]
    private partial void LogUnauthorizedCreateAttempt();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Validation failed creating sensor {SensorName}. Errors: {@ValidationErrors}")]
    private partial void LogCreateValidationFailed(string sensorName, object validationErrors);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sensor successfully created with ID: {SensorId}")]
    private partial void LogSensorCreated(string sensorId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting update for sensor ID: {SensorId}")]
    private partial void LogStartingSensorUpdate(string sensorId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unauthorized attempt to update sensor {SensorId}. Lacks Super/Admin/System privileges.")]
    private partial void LogUnauthorizedUpdateAttempt(string sensorId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Validation failed updating sensor {SensorId}. Errors: {@ValidationErrors}")]
    private partial void LogUpdateValidationFailed(string sensorId, object validationErrors);

    [LoggerMessage(Level = LogLevel.Error, Message = "Update failed. Sensor ID {SensorId} not found.")]
    private partial void LogSensorUpdateFailedNotFound(string sensorId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sensor {SensorId} successfully updated.")]
    private partial void LogSensorUpdated(string sensorId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Starting deletion of sensor ID: {SensorId}")]
    private partial void LogStartingSensorDeletion(string sensorId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Unauthorized attempt to delete sensor {SensorId}. Lacks Super/Admin/System privileges.")]
    private partial void LogUnauthorizedDeleteAttempt(string sensorId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sensor {SensorId} successfully deleted.")]
    private partial void LogSensorDeleted(string sensorId);
}
