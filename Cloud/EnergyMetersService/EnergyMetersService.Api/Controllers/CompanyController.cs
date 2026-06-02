using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Application.Interfaces;
using EnergyMetersService.Application.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace EnergyMetersService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
// [Authorize] // Descomentar cuando tengas configurado el login/JWT
public class CompanyController(ICompanyService companyAppService) : ControllerBase
{
    private readonly ICompanyService _companyAppService = companyAppService;

    // GET: api/company
    [HttpGet]
    public ActionResult<IEnumerable<CompanyDto>> GetAll()
    {
        // Materializamos la consulta a una lista
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
            return NotFound(new { Message = ex.Message });
        }
    }

    // POST: api/company
    [HttpPost]
    public async Task<ActionResult> Create([FromBody] CompanyCreateDto dto)
    {
        try
        {
            var newId = await _companyAppService.CreateCompanyAsync(dto);

            // Devuelve un 201 Created y la ruta para consultar el nuevo registro
            return CreatedAtAction(nameof(GetById), new { id = newId }, new { Id = newId });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message); // 403
        }
        catch (ValidationException ex)
        {
            // Devuelve un 400 Bad Request con la lista de errores de FluentValidation
            return BadRequest(new { Errors = ex.Errors.Select(e => e.ErrorMessage) });
        }
    }

    // PUT: api/company/{id}
    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] CompanyUpdateDto dto)
    {
        try
        {
            await _companyAppService.UpdateCompanyAsync(id, dto);
            return NoContent(); // 204 No Content (es el estándar para un update exitoso)
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { Message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid(ex.Message);
        }
        catch (ValidationException ex)
        {
            return BadRequest(new { Errors = ex.Errors.Select(e => e.ErrorMessage) });
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
            return Forbid(ex.Message);
        }
    }
}
