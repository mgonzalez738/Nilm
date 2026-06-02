using EnergyMetersService.Domain.Entities;

namespace EnergyMetersService.Domain.Interfaces;

public interface ITimeSeriesRepository<TSeries> where TSeries : TimeSeries
{
    IQueryable<TSeries> AsQueryable();

    Task AddAsync(TSeries data);
    Task AddManyAsync(IEnumerable<TSeries> dataList);
    Task DeleteBySensorIdAsync(string sensorId);
}
