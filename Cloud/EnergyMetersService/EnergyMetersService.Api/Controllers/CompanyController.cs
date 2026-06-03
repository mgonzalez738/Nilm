using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMetersService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CompanyController(ICompanyService companyAppService) : ControllerBase
{
    private readonly ICompanyService _companyAppService = companyAppService;

    // GET: api/company
    [HttpGet]
    public ActionResult<IEnumerable<CompanyDto>> GetAll()
    {
        var companies = _companyAppService.GetQueryable().ToList();
        return Ok(companies);
    }

    // GET: api/company/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<CompanyDto>> GetById(string id)
    {
        try
        {
            var company = await _companyAppService.GetByIdAsync(id);
            return Ok(company);
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

    // POST: api/company
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CompanyCreateDto dto)
    {
        try
        {
            var newId = await _companyAppService.CreateCompanyAsync(dto);
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

    // PUT: api/company/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] CompanyUpdateDto dto)
    {
        try
        {
            await _companyAppService.UpdateCompanyAsync(id, dto);
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

    // DELETE: api/company/{id}
    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            await _companyAppService.DeleteCompanyAsync(id);
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
        // Agrupa los errores por nombre de propiedad (ej: "Name" -> ["El nombre es requerido", "Debe tener al menos 3 letras"])
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