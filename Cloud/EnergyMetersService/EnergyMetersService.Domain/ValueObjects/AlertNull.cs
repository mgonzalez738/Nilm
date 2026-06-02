namespace EnergyMetersService.Domain.ValueObjects;

public record AlertNull (
    string AlertGuid,
    string Property,
    DateTime Timestamp);