using EnergyMetersService.Application.Interfaces;
using System.Security.Claims;

namespace EnergyMetersService.Api.Services;

public class ApiUserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    // 1. Mapeado al claim "user_id" exacto que inyecta el AuthHandler
    public string UserId => User?.FindFirst("user_id")?.Value ?? string.Empty;

    // 2. Mapeado al claim "client_id"
    public string ClientId => User?.FindFirst("client_id")?.Value ?? string.Empty;

    // 3. Los roles ahora son una colección, recuperando todos los ClaimTypes.Role
    public IEnumerable<string> Roles =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? [];

    // 4. Mapeado al claim "company_id" (en minúsculas y con guion bajo)
    public string CompanyId => User?.FindFirst("company_id")?.Value ?? string.Empty;

    // 5. Mapeado al claim "project_id" (singular, como lo guarda el AuthHandler)
    public IEnumerable<string> ProjectIds =>
        User?.FindAll("project_id").Select(c => c.Value) ?? [];

    // 6. No es el sistema, siempre false para este contexto de API
    public bool IsSystem => false;
}