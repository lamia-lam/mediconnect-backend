using System.Text.Json;
using StackExchange.Redis;

namespace MedConnect.Services
{
    public class RedisCache : ICache
    {
        private readonly IDatabase _db;
        private readonly ILogger<RedisCache> _logger;

        public RedisCache(IConnectionMultiplexer connectionMultiplexer, ILogger<RedisCache> logger)
        {
            _db = connectionMultiplexer.GetDatabase();
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key)
        {
            var value = await _db.StringGetAsync(key);
            if (value.HasValue)
            {
                _logger.LogInformation("Redis cache hit for key: {Key}", key);
                return JsonSerializer.Deserialize<T>(value!);
            }
            _logger.LogInformation("Redis cache miss for key: {Key}", key);
            return default;
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
        {
            var json = JsonSerializer.Serialize(value);
            await _db.StringSetAsync(key, json, expiry);
            _logger.LogInformation("Redis cache set for key: {Key} with expiry: {Expiry}", key, expiry);
        }

        public async Task RemoveAsync(string key)
        {
            await _db.KeyDeleteAsync(key);
            _logger.LogInformation("Redis cache removed for key: {Key}", key);
        }
    }
}