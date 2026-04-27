namespace CryptoSolution.Tests;

public class FakeCache : ICache
{
    private readonly Dictionary<string, object> _storage = new();

    public ValueTask AddAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        _storage[key] = value!;
        return ValueTask.CompletedTask;
    }

    public ValueTask<T?> GetAsync<T>(string key)
    {
        return _storage.TryGetValue(key, out var value)
            ? ValueTask.FromResult((T?)value)
            : ValueTask.FromResult(default(T));
    }

    public async ValueTask<T?> GetOrAddAsync<T>(string key, Func<Task<T>> valueFactory, TimeSpan? ttl = null)
    {
        if (_storage.TryGetValue(key, out var value))
        {
            return (T)value;
        }

        var newValue = await valueFactory();
        if (newValue != null)
        {
            _storage[key] = newValue;
        }
        return newValue;
    }

    public ValueTask Remove(string key)
    {
        _storage.Remove(key);
        return ValueTask.CompletedTask;
    }
}
