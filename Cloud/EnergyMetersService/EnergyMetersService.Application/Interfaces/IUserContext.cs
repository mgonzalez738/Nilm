namespace EnergyMetersService.Application.Interfaces;

public interface IUserContext
{
    string UserId { get; }
    string Role { get; }
    string? CompanyId { get; }
    IEnumerable<string> ProjectIds { get; }

    bool IsSystem { get; }
}