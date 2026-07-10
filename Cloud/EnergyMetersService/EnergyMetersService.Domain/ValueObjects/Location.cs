namespace EnergyMetersService.Domain.ValueObjects;

public record Location()
{
    public bool Enabled = false;
    public double Latitude = 0.0;
    public double Longitude = 0.0;
}