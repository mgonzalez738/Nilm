using EnergyMetersService.Application.Interfaces;
using System.Security.Claims;

namespace EnergyMetersService.Api.Services;

public class ApiUserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    // Se mapea comúnmente al 'sub' (subject) del token JWT o al NameIdentifier
    public string UserId => User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

    public string Role => User?.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

    public string? CompanyId => User?.FindFirst("CompanyId")?.Value;

    public IEnumerable<string> ProjectIds =>
        User?.FindAll("ProjectId").Select(c => c.Value) ?? [];

    public bool IsSystem => false;
}