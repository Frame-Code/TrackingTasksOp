namespace Application.Ports.Cache;

public interface IRedisCache
{
    public Task Save<T>(string key, T value, TimeSpan historyTtl);
    public Task<T?> Get<T>(string key);
    public Task Delete(string key);
}