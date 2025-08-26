using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace MedConnect.Services
{
    public class InMemoryCache : ICache
    {
        private readonly ConcurrentDictionary<string, (object Value, DateTime? Expiry)> _cache = new();
        private readonly ILogger<InMemoryCache> _logger;

        public InMemoryCache(ILogger<InMemoryCache> logger)
        {
            _logger = logger;
        }

        public Task<T?> GetAsync<T>(string key)
        {
            if (_cache.TryGetValue(key, out var entry))
            {
                if (entry.Expiry == null || entry.Expiry > DateTime.UtcNow)
                {
                    _logger.LogInformation("Cache hit for key: {Key}", key);
                    return Task.FromResult((T?)entry.Value);
                }
                else
                {
                    _logger.LogInformation("Cache expired for key: {Key}", key);
                    _cache.TryRemove(key, out _);
                }
            }
            _logger.LogInformation("Cache miss for key: {Key}", key);
            return Task.FromResult(default(T));
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var expiryDate = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
            _cache[key] = (value!, expiryDate);
            _logger.LogInformation("Cache set for key: {Key} with expiry: {Expiry}", key, expiryDate);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            _cache.TryRemove(key, out _);
            _logger.LogInformation("Cache removed for key: {Key}", key);
            return Task.CompletedTask;
        }
    }
}