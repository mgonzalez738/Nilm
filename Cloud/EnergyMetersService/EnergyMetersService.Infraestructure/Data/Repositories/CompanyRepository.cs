using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Domain.Interfaces;
using EnergyMetersService.Infraestructure.Data.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EnergyMetersService.Infraestructure.Data.Repositories;

public class CompanyRepository(MongoDbContext context) 
    : MongoDbEntityRepository<Company>(context), ICompanyRepository
{
    public async Task<bool> ExistsByNameAsync(string name, string? ignoreId = null)
    {
        var sanitizedName = name.Trim();

        var filter = Builders<Company>.Filter.Regex(
            c => c.Name,
            new BsonRegularExpression($"^{sanitizedName}$", "i")
        );

        if (!string.IsNullOrWhiteSpace(ignoreId))
        {
            filter &= Builders<Company>.Filter.Ne(c => c.Id, ignoreId);
        }

        return await _collection.Find(filter).AnyAsync();
    }
}