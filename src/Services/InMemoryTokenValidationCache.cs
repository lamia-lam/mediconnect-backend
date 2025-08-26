using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace MedConnect.Services
{
    public class InMemoryTokenValidationCache: ITokenValidationCache
    {
        private readonly ILogger<InMemoryTokenValidationCache> _logger;
        private readonly IMemoryCache _cache;

        // Store revoked JTIs per user
        private readonly ConcurrentDictionary<string, HashSet<string>> _revokedJtis
            = new(StringComparer.OrdinalIgnoreCase);

        private const string RefreshTokenPrefix = "refresh_token:";

        public InMemoryTokenValidationCache(ILogger<InMemoryTokenValidationCache> logger, IMemoryCache? cache = null)
        {
            _logger = logger;
            _cache = cache ?? new MemoryCache(new MemoryCacheOptions());
        }

        public Task<bool> IsJtiRevokedAsync(string username, string jti)
        {
            if (_revokedJtis.TryGetValue(username, out var jtis))
            {
                return Task.FromResult(jtis.Contains(jti));
            }
            return Task.FromResult(false);
        }

        public Task AddRevokedJtiAsync(string username, string jti)
        {
            var jtiSet = _revokedJtis.GetOrAdd(username, _ => new HashSet<string>(StringComparer.Ordinal));
            lock (jtiSet) // HashSet is not thread-safe
            {
                jtiSet.Add(jti);
            }

            _logger.LogInformation("Added JTI {Jti} to revoked list for user {Username}", jti, username);
            return Task.CompletedTask;
        }

        public Task<bool> IsRefreshTokenValidAsync(string token)
        {
            var key = $"{RefreshTokenPrefix}{token}";
            if (_cache.TryGetValue(key, out bool isValid))
            {
                return Task.FromResult(isValid);
            }
            return Task.FromResult(false);
        }

        public Task SetRefreshTokenValidityAsync(string token, bool isValid, TimeSpan expiry)
        {
            var key = $"{RefreshTokenPrefix}{token}";
            _cache.Set(key, isValid, expiry);

            _logger.LogInformation("Set refresh token {Token} validity to {IsValid}", token, isValid);
            return Task.CompletedTask;
        }
    }
}
