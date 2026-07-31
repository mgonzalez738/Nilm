using EnergyMetersService.Domain.Entities;
using EnergyMetersService.Domain.Interfaces;
using EnergyMetersService.Infraestructure.Data.Models;
using MongoDB.Driver;
using System.Linq.Expressions;

namespace EnergyMetersService.Infraestructure.Data.Repositories
{
    public abstract class MongoDbEntityRepository<TEntity>(MongoDbContext context)
    : IEntityRepository<TEntity> where TEntity : Entity
    {
        protected readonly IMongoCollection<TEntity> _collection = context.GetCollection<TEntity>(typeof(TEntity).Name);

        public virtual async Task<TEntity?> GetByIdAsync(string id)
        {
            var filter = Builders<TEntity>.Filter.Eq(e => e.Id, id);
            return await _collection.Find(filter).FirstOrDefaultAsync();
        }

        public virtual IQueryable<TEntity> AsQueryable()
        {
            return _collection.AsQueryable();
        }

        public virtual async Task AddAsync(TEntity entity)
        {
            await _collection.InsertOneAsync(entity);
        }

        public virtual async Task UpdateAsync(TEntity entity)
        {
            var filter = Builders<TEntity>.Filter.Eq(e => e.Id, entity.Id);
            await _collection.ReplaceOneAsync(filter, entity);
        }

        public virtual async Task DeleteAsync(string id)
        {
            var filter = Builders<TEntity>.Filter.Eq(e => e.Id, id);
            await _collection.DeleteOneAsync(filter);
        }

        public virtual async Task DeleteManyAsync(Expression<Func<TEntity, bool>> predicate)
        {
            await _collection.DeleteManyAsync(predicate);
        }

        public virtual async Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate)
        {
            return await _collection.Find(predicate).AnyAsync();
        }
    }
}