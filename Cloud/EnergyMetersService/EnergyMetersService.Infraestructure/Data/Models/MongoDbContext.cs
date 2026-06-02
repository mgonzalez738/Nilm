using EnergyMetersService.Infraestructure.Data.Interfaces;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EnergyMetersService.Infraestructure.Data.Models;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoDbSettings> settings)
    {
        var client = new MongoClient(settings.Value.ConnectionString);
        _database = client.GetDatabase(settings.Value.DatabaseName);

        var configurations = typeof(MongoDbContext).Assembly.GetTypes()
        .Where(t => typeof(IMongoConfiguration).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var type in configurations)
        {
            var configuration = (IMongoConfiguration)Activator.CreateInstance(type)!;
            configuration.Apply();
        }
    }

    public IMongoCollection<T> GetCollection<T>(string name)
    {
        return _database.GetCollection<T>(name);
    }
}
