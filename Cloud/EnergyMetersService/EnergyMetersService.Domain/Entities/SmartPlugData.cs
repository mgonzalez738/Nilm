namespace EnergyMetersService.Domain.Entities;

public record SmartPlugData : TimeSeries
{
    public double? Voltage { get; init; }
    public double? Current { get; init; }
    public double? ActivePower { get; init; }
    public double? ReactivePower { get; init; }
    public List<int>? Status { get; init; }
}