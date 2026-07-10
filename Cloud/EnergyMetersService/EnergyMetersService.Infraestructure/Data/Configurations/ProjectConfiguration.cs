using MongoDB.Bson.Serialization;

using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Infraestructure.Data.Interfaces;

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
