using StackExchange.Redis;
using System.Text.Json;

namespace MedConnect.Services
{
    public interface ITokenValidationCache
    {
        Task<bool> IsJtiRevokedAsync(string username, string jti);
        Task AddRevokedJtiAsync(string username, string jti);
        Task<bool> IsRefreshTokenValidAsync(string token);
        Task SetRefreshTokenValidityAsync(string token, bool isValid, TimeSpan expiry);
    }

    public class TokenValidationCache : ITokenValidationCache
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<TokenValidationCache> _logger;
        private const string RevokedJtiPrefix = "revoked_jti:";
        private const string RefreshTokenPrefix = "refresh_token:";

        public TokenValidationCache(IConnectionMultiplexer redis, ILogger<TokenValidationCache> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        public async Task<bool> IsJtiRevokedAsync(string username, string jti)
        {
            var db = _redis.GetDatabase();
            var key = $"{RevokedJtiPrefix}{username}";
            var revokedJtis = await db.SetMembersAsync(key);
            return revokedJtis.Any(x => x.ToString() == jti);
        }

        public async Task AddRevokedJtiAsync(string username, string jti)
        {
            var db = _redis.GetDatabase();
            var key = $"{RevokedJtiPrefix}{username}";
            await db.SetAddAsync(key, jti);
            // Set expiry to 7 days to match refresh token expiry
            await db.KeyExpireAsync(key, TimeSpan.FromDays(7));
            _logger.LogInformation("Added JTI {Jti} to revoked list for user {Username}", jti, username);
        }

        public async Task<bool> IsRefreshTokenValidAsync(string token)
        {
            var db = _redis.GetDatabase();
            var key = $"{RefreshTokenPrefix}{token}";
            var value = await db.StringGetAsync(key);
            return value.HasValue && JsonSerializer.Deserialize<bool>(value!);
        }

        public async Task SetRefreshTokenValidityAsync(string token, bool isValid, TimeSpan expiry)
        {
            var db = _redis.GetDatabase();
            var key = $"{RefreshTokenPrefix}{token}";
            await db.StringSetAsync(key, JsonSerializer.Serialize(isValid), expiry);
            _logger.LogInformation("Set refresh token {Token} validity to {IsValid}", token, isValid);
        }
    }
}