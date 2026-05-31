namespace SmartPlugService.Domain.ValueObjects;

public record Location(
    bool Enabled, 
    double Latitude, 
    double Longitude);