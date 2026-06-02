using EnergyMetersService.Domain.Entities;
using System.Linq.Expressions;

namespace EnergyMetersService.Domain.Interfaces;

public interface IEntityRepository<TEntity> where TEntity : Entity
{
    Task<TEntity?> GetByIdAsync(string id);
    IQueryable<TEntity> AsQueryable();

    Task AddAsync(TEntity entity);
    Task UpdateAsync(TEntity entity);
    Task DeleteAsync(string id);

    Task DeleteManyAsync(Expression<Func<TEntity, bool>> predicate);
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate);
}