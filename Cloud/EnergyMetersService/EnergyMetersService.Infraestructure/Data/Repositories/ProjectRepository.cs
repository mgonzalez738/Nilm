using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Domain.Interfaces;
using EnergyMetersService.Infraestructure.Data.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Linq;

namespace EnergyMetersService.Infraestructure.Data.Repositories;

public class ProjectRepository(MongoDbContext context) 
    : MongoDbEntityRepository<Project>(context), IProjectRepository
{
    private readonly IMongoCollection<Company> _companyCollection = context.GetCollection<Company>(typeof(Company).Name);

    public override async Task<Project?> GetByIdAsync(string id)
    {
        return await GetBaseQuery().FirstOrDefaultAsync(x => x.Id == id);
    }

    public override IQueryable<Project> AsQueryable()
    {
        return GetBaseQuery();
    }

    public async Task<bool> ExistsByNameAsync(string name, string? companyId = null, string? ignoreId = null)
    {
        var sanitizedName = name.Trim();

        var filter = Builders<Project>.Filter.Regex(
            c => c.Name,
            new BsonRegularExpression($"^{sanitizedName}$", "i")
        );

        if (!string.IsNullOrWhiteSpace(companyId))
        {
            filter &= Builders<Project>.Filter.Eq(c => c.CompanyId, companyId);
        }

        if (!string.IsNullOrWhiteSpace(ignoreId))
        {
            filter &= Builders<Project>.Filter.Ne(c => c.Id, ignoreId);
        }

        return await _collection.Find(filter).AnyAsync();
    }

    private IQueryable<Project> GetBaseQuery()
    {
        return from p in _collection.AsQueryable()
               join c in _companyCollection.AsQueryable() on p.CompanyId equals c.Id into companyGroup
               from company in companyGroup.DefaultIfEmpty()
               select new Project
               {
                   Id = p.Id,
                   Name = p.Name,
                   CompanyId = p.CompanyId,
                   Company = company
               };
    }
} 