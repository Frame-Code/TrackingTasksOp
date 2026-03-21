using System.Linq.Expressions;

namespace Application.Ports.Repositories;

public interface IRepository <T>
{
    Task<IEnumerable<T>> GetAllAsync(Expression<Func<T, bool>>? filter, bool tracking = false);
    Task<T?> GetByIdAsync(int id, bool tracking = false);
    Task<T> SaveAsync(T task);
}