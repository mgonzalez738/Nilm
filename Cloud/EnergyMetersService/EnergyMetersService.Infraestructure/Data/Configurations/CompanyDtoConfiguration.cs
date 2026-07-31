using EnergyMetersService.Application.DTOs;
using EnergyMetersService.Infraestructure.Data.Interfaces;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace EnergyMetersService.Infraestructure.Data.Configurations;

internal class CompanyDtoConfiguration : IMongoConfiguration
{
    public void Apply()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(CompanyDto)))
        {
            BsonClassMap.RegisterClassMap<CompanyDto>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(c => c.Id)
                  .SetSerializer(new StringSerializer(BsonType.ObjectId));
            });
        }
    }
} 