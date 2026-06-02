using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Infraestructure.Data.Interfaces;
using MongoDB.Bson.Serialization;

namespace EnergyMetersService.Infraestructure.Data.Configurations;

internal class CompanyConfiguration : IMongoConfiguration
{
    public void Apply()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(Company)))
        {
            BsonClassMap.RegisterClassMap<Company>(cm =>
            {
                cm.AutoMap();
            });
        }
    }
}
