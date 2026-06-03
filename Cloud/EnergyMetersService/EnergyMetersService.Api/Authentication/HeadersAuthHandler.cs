using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace EnergyMetersService.Api.Authentication;

// La clase DEBE ser partial para usar [LoggerMessage]
public partial class HeadersAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
                                        ILoggerFactory loggerFactory,
                                        ILogger<HeadersAuthHandler> logger,
                                        UrlEncoder encoder) 
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, loggerFactory, encoder)
{
    private readonly ILogger<HeadersAuthHandler> _logger = logger;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Validar UserId
        if (!Context.Request.Headers.TryGetValue("UserId", out var userIdValues) || string.IsNullOrWhiteSpace(userIdValues))
        {
            LogMissingUserId();
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var userId = userIdValues.ToString();

        if (!ObjectId.TryParse(userId, out _))
        {
            LogInvalidUserIdFormat(userId);
            return Task.FromResult(AuthenticateResult.Fail("Invalid UserId format."));
        }

        List<Claim> claims = [new Claim("user_id", userId)];

        // 2. ClientId
        if (Context.Request.Headers.TryGetValue("ClientId", out var clientIdValues) && !string.IsNullOrWhiteSpace(clientIdValues))
        {
            claims.Add(new Claim("client_id", clientIdValues.ToString()));
        }

        // 3. Roles
        if (Context.Request.Headers.TryGetValue("Roles", out var roleValues) && !string.IsNullOrWhiteSpace(roleValues))
        {
            var roles = roleValues.ToString().Split(',');
            foreach (var role in roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
                }
            }
        }

        // 4. Validar CompanyId
        if (!Context.Request.Headers.TryGetValue("CompanyId", out var companyIdValues) || string.IsNullOrWhiteSpace(companyIdValues))
        {
            LogMissingCompanyId(userId);
            return Task.FromResult(AuthenticateResult.Fail("CompanyId header is required."));
        }

        var companyId = companyIdValues.ToString();
        if (!ObjectId.TryParse(companyId, out _))
        {
            LogInvalidCompanyIdFormat(companyId, userId);
            return Task.FromResult(AuthenticateResult.Fail("Invalid CompanyId format."));
        }

        claims.Add(new Claim("company_id", companyId));

        // 5. Validar ProjectIds
        if (Context.Request.Headers.TryGetValue("ProjectIds", out var projectIdValues) && !string.IsNullOrWhiteSpace(projectIdValues))
        {
            var projectIds = projectIdValues.ToString().Split(',');
            foreach (var projectId in projectIds)
            {
                var trimmedProjectId = projectId.Trim();

                if (!string.IsNullOrWhiteSpace(trimmedProjectId))
                {
                    if (!ObjectId.TryParse(trimmedProjectId, out _))
                    {
                        LogInvalidProjectIdFormat(trimmedProjectId, userId);
                        return Task.FromResult(AuthenticateResult.Fail($"Invalid ProjectId format: '{trimmedProjectId}'."));
                    }

                    claims.Add(new Claim("project_id", trimmedProjectId));
                }
            }
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        // Éxito
        LogAuthenticationSuccess(userId, claims.Count);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    // --- High-Performance Logging Methods ---

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authentication failed: Missing or empty UserId header.")]
    private partial void LogMissingUserId();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authentication failed: UserId '{UserId}' is not a valid ObjectId.")]
    private partial void LogInvalidUserIdFormat(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authentication failed for UserId '{UserId}': CompanyId header is required but missing.")]
    private partial void LogMissingCompanyId(string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authentication failed for UserId '{UserId}': CompanyId '{CompanyId}' is not a valid ObjectId.")]
    private partial void LogInvalidCompanyIdFormat(string companyId, string userId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Authentication failed for UserId '{UserId}': ProjectId '{ProjectId}' is not a valid ObjectId.")]
    private partial void LogInvalidProjectIdFormat(string projectId, string userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Identity successfully generated for UserId {UserId} with {ClaimCount} claims.")]
    private partial void LogAuthenticationSuccess(string userId, int claimCount);
}