using EnergyMetersService.Api.Extensions;
using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Application.Interfaces;
using FluentValidation;
using Mapster;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.OData.Query;
using MongoDB.AspNetCore.OData;

namespace EnergyMetersService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SmartPlugSensorController(
    ISmartPlugSensorService sensorAppService,
    ICompanyService companyAppService) : ControllerBase
{
    private readonly ISmartPlugSensorService _sensorAppService = sensorAppService;
    private readonly ICompanyService _companyAppService = companyAppService;

    // GET: api/smartplugsensor
    [HttpGet]
    //[MongoEnableQuery]
    public ActionResult GetAll()
    {
        var query = _sensorAppService.GetQueryable();

        return Ok(query);
    }

    // GET: api/smartplugsensor/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<SmartPlugSensorDto>> GetById(string id)
    {
        try
        {
            // El servicio ya devuelve el DTO armado y con la compañía expandida
            var sensor = await _sensorAppService.GetByIdAsync(id);

            return Ok(sensor);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found"
            );
        }
    }

    // POST: api/smartplugsensor
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] SmartPlugSensorCreateDto dto)
    {
        try
        {
            var newId = await _sensorAppService.CreateSensorAsync(dto);
            return CreatedAtAction(nameof(GetById), new { id = newId }, new { Id = newId });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden Access"
            );
        }
        catch (ValidationException ex)
        {
            return MapValidationException(ex);
        }
    }

    // PUT: api/smartplugsensor/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] SmartPlugSensorUpdateDto dto)
    {
        try
        {
            await _sensorAppService.UpdateSensorAsync(id, dto);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource Not Found"
            );
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden Access"
            );
        }
        catch (ValidationException ex)
        {
            return MapValidationException(ex);
        }
    }

    // DELETE: api/smartplugsensor/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            await _sensorAppService.DeleteSensorAsync(id);
            return NoContent();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status403Forbidden,
                title: "Forbidden Access"
            );
        }
    }

    /// <summary>
    /// Helper method to map FluentValidation exceptions to standard RFC 7807 ValidationProblemDetails.
    /// </summary>
    private ActionResult MapValidationException(ValidationException ex)
    {
        var errorDictionary = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()
            );

        var problemDetails = new ValidationProblemDetails(errorDictionary)
        {
            Title = "Validation Failed",
            Detail = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest
        };

        return ValidationProblem(problemDetails);
    }
}
