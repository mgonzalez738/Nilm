using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Infraestructure.Data.Interfaces;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;

namespace EnergyMetersService.Infraestructure.Data.Configurations;

internal class EntityConfiguration : IMongoConfiguration
{
    public void Apply()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(Entity)))
        {
            BsonClassMap.RegisterClassMap<Entity>(cm =>
            {
                cm.AutoMap();
                cm.MapIdProperty(e => e.Id)
                  .SetIdGenerator(StringObjectIdGenerator.Instance)
                  .SetSerializer(new StringSerializer(BsonType.ObjectId));
            });
        }
    }
}
