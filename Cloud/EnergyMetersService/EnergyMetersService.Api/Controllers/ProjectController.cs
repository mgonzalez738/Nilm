using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Results;
using Microsoft.OData;

namespace EnergyMetersService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "RequireAccessRoles")]
public class ProjectController(IProjectService projectAppService) : ControllerBase
{
    private readonly IProjectService _projectAppService = projectAppService;

    [HttpGet]
    public ActionResult GetAll(ODataQueryOptions<ProjectDto> options)
    {
        try
        {
            var query = _projectAppService.GetQueryable();

            long? totalCount = null;
            if (options.Count?.Value == true)
            {
                var countQuery = options.Filter != null
                    ? options.Filter.ApplyTo(query, new ODataQuerySettings()) as IQueryable<ProjectDto>
                    : query;

                totalCount = countQuery?.Count();
            }

            var dbQuery = options.ApplyTo(query, AllowedQueryOptions.Select | AllowedQueryOptions.Expand) as IQueryable<ProjectDto>;


            var results = dbQuery?.ToList() ?? [];

            IQueryable finalResults = results.AsQueryable();
            if (options.SelectExpand != null)
            {
                finalResults = options.SelectExpand.ApplyTo(finalResults, new ODataQuerySettings());
            }

            if (totalCount.HasValue)
            {
                return Ok(new PageResult<object>(
                    finalResults.Cast<object>(),
                    null,
                    totalCount
                ));
            }

            return Ok(finalResults);
        }
        catch (ODataException ex)
        {
            return Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid OData query."
            );
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectDto>> GetById(string id)
    {
        try
        {
            var project = await _projectAppService.GetByIdAsync(id);
            return Ok(project);
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

    [HttpPost]
    public async Task<ActionResult> Create([FromBody] ProjectCreateDto dto)
    {
        try
        {
            var newId = await _projectAppService.CreateAsync(dto);
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

    [HttpPut("{id}")]
    public async Task<ActionResult> Update(string id, [FromBody] ProjectUpdateDto dto)
    {
        try
        {
            await _projectAppService.UpdateAsync(id, dto);
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

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(string id)
    {
        try
        {
            await _projectAppService.DeleteAsync(id);
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