using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace CryptoSolution;

public interface ICache
{
    ValueTask AddAsync<T>(string key, T value, TimeSpan? ttl = null);
    ValueTask<T?> GetAsync<T>(string key);
    ValueTask<T?> GetOrAddAsync<T>(string key, Func<Task<T>> valueFactory, TimeSpan? ttl = null);
    ValueTask RemoveAsync(string key);
}

/// <summary>
/// Für Demonstartionszwecke implementieren wir hier eine einfache MemoryCache-Adapterklasse, die das ICache-Interface verwendet. In einer echten Anwendung kann man sie durch einen Redis- oder KeyDb-Adapter ersetzen, ohne dass der Rest des Codes angepasst werden muss. Methoden nutzen ValueTask für bessere Performance bei synchronen Rückgaben.
/// </summary>
public class MemoryCacheAdapter(IMemoryCache cache) : ICache
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = 
        new(Environment.ProcessorCount * 2, 1000);

    public ValueTask AddAsync<T>(string key, T value, TimeSpan? ttl = null)
    {
        cache.Set(key, value, ttl ?? TimeSpan.FromMinutes(5));
        return ValueTask.CompletedTask;
    }

    public ValueTask<T?> GetAsync<T>(string key)
    {
        if (cache.TryGetValue(key, out T? value))
        {
            return ValueTask.FromResult(value);
        }
        return ValueTask.FromResult<T?>(default);
    }

    /// <summary>
    /// Ruft einen Wert aus dem Cache ab oder generiert ihn asynchron, falls er noch nicht existiert.
    /// Verhindert "Cache Stampedes" durch atomares Locking. Nutzt ValueTask für bessere Performance bei synchronen Rückgaben. Die Gültigkeitsdauer des Eintrags kann optional angegeben werden, standardmäßig 5 Minuten.
    /// </summary>
    /// <typeparam name="T">Der Typ des im Cache gespeicherten Wertes.</typeparam>
    /// <param name="key">Der eindeutige Schlüssel des Cache-Eintrags.</param>
    /// <param name="valueFactory">Eine asynchrone Funktion, die den Wert generiert, falls ein Cache-Miss auftritt.</param>
    /// <param name="ttl">Die Gültigkeitsdauer des Eintrags. Standardmäßig 5 Minuten, falls null.</param>
    /// <returns>Den zwischengespeicherten oder neu generierten Wert.</returns>
    public async ValueTask<T?> GetOrAddAsync<T>(string key, Func<Task<T>> valueFactory, TimeSpan? ttl = null)
    {
        if (cache.TryGetValue(key, out T? value))
        {
            return value;
        }

        var myLock = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await myLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (cache.TryGetValue(key, out value))
            {
                return value;
            }

            value = await valueFactory().ConfigureAwait(false);

            if (value != null)
            {
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl ?? TimeSpan.FromMinutes(5)
                };
                cache.Set(key, value, options);
            }

            return value;
        }
        finally
        {
            myLock.Release();
        }
    }

    public ValueTask RemoveAsync(string key)
    {
        cache.Remove(key);
        _locks.TryRemove(key, out _);
        return ValueTask.CompletedTask;
    }
}
