using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;

using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Infraestructure.Data.Interfaces;

namespace EnergyMetersService.Infraestructure.Data.Configurations;

internal class ProjectConfiguration : IMongoConfiguration
{
    public void Apply()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(Project)))
        {
            BsonClassMap.RegisterClassMap<Project>(cm =>
            {
                cm.AutoMap();
                cm.MapProperty(p => p.CompanyId)
                  .SetSerializer(new StringSerializer(BsonType.ObjectId));
                cm.UnmapProperty(p => p.Company);
            });
        }
    }
}
