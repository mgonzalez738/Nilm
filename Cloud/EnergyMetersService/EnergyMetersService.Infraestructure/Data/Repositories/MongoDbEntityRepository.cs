using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Domain.Interfaces;
using EnergyMetersService.Infraestructure.Data.Models;
using MongoDB.Driver;
using System.Linq.Expressions;

namespace EnergyMetersService.Infraestructure.Data.Repositories
{
    public class MongoDbEntityRepository<TEntity>(MongoDbContext context)
    : IEntityRepository<TEntity> where TEntity : Entity
    {
        private readonly IMongoCollection<TEntity> _collection = context.GetCollection<TEntity>(typeof(TEntity).Name);

        public async Task<TEntity?> GetByIdAsync(string id)
        {
            var filter = Builders<TEntity>.Filter.Eq(e => e.Id, id);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public IQueryable<TEntity> AsQueryable()
        {
            return _collection.AsQueryable();
        }

        public async Task AddAsync(TEntity entity)
        {
            await _collection.InsertOneAsync(entity);
        }

        public async Task UpdateAsync(TEntity entity)
        {
            var filter = Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id);
            await _collection.ReplaceOneAsync(filter, entity);
        }

        public async Task DeleteAsync(string id)
        {
            var filter = Builders<TEntity>.Filter.Eq(e => e.Id, id);
            await _collection.DeleteOneAsync(filter);
        }

        public async Task DeleteManyAsync(Expression<Func<TEntity, bool>> predicate)
        {
            await _collection.DeleteManyAsync(predicate);
        }

        public async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _collection.Find(predicate).AnyAsync();
        }
    }
}
