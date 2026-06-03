namespace EnergyMetersService.Application.Interfaces;

public interface IUserContext
{
    string UserId { get; }
    string ClientId { get; }
    IEnumerable<string> Roles { get; }
    string CompanyId { get; }
    IEnumerable<string> ProjectIds { get; }

    bool IsSystem { get; }
}