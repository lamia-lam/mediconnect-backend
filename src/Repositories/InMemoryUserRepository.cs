using MedConnect.Models;
using MedConnect.Services;
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace MedConnect.Repositories
{
    public class InMemoryUserRepository : IUserRepository
    {
        private readonly ILogger<InMemoryUserRepository> _logger;
        private readonly ITokenValidationCache _tokenCache;

        // Backing store: users by int Id
        private readonly ConcurrentDictionary<int, User> _users = new();

        // Per-user locks to protect list/set mutations
        private readonly ConcurrentDictionary<int, object> _locks = new();

        // Simple auto-increment for new users (start at 1)
        private int _nextId = 0;

        public InMemoryUserRepository(ILogger<InMemoryUserRepository> logger, ITokenValidationCache tokenCache)
        {
            _logger = logger;
            _tokenCache = tokenCache;
        }

        private object GetLock(int id) => _locks.GetOrAdd(id, _ => new object());

        // ---------------------------
        // IRepository<User> (common)
        // ---------------------------

        public Task<User?> GetByIdAsync(int id)
            => Task.FromResult(_users.TryGetValue(id, out var user) ? user : null);

        public Task<IEnumerable<User>> GetAllAsync()
            => Task.FromResult(_users.Values.AsEnumerable());

        public async Task AddAsync(User entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            if (entity.Id <= 0)
                entity.Id = Interlocked.Increment(ref _nextId);

            entity.RefreshTokens ??= new List<RefreshToken>();
            entity.RevokedJtis ??= new HashSet<string>(StringComparer.Ordinal);

            _users[entity.Id] = entity;
            await Task.CompletedTask;
        }

        public async Task UpdateAsync(User entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            if (entity.Id <= 0) throw new InvalidOperationException("User.Id must be a positive int.");

            _users[entity.Id] = entity;
            await Task.CompletedTask;
        }

        public async Task DeleteAsync(User entity)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            _users.TryRemove(entity.Id, out _);
            _locks.TryRemove(entity.Id, out _);
            await Task.CompletedTask;
        }

        // If your IRepository<T> has more members, these helpers are here and safe to keep:
        public async Task<IEnumerable<User>> FindAsync(Expression<Func<User, bool>> predicate)
            => await Task.FromResult(_users.Values.Where(predicate.Compile()).AsEnumerable());

        public async Task<bool> AnyAsync(Expression<Func<User, bool>> predicate)
            => await Task.FromResult(_users.Values.Any(predicate.Compile()));

        // ---------------------------
        // IUserRepository
        // ---------------------------

        public async Task<User?> GetByUsernameAsync(string username)
        {
            var user = _users.Values.FirstOrDefault(u =>
                string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
            return await Task.FromResult(user);
        }

        public async Task<User?> GetByEmailAsync(string email)
        {
            var user = _users.Values.FirstOrDefault(u =>
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase));
            return await Task.FromResult(user);
        }

        public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
        {
            var user = _users.Values.FirstOrDefault(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken));
            return await Task.FromResult(user);
        }

        public async Task AddRefreshTokenAsync(User user, RefreshToken refreshToken)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (refreshToken == null) throw new ArgumentNullException(nameof(refreshToken));

            lock (GetLock(user.Id))
            {
                user.RefreshTokens ??= new List<RefreshToken>();
                user.RefreshTokens.Add(refreshToken);
            }

            // mirror your original semantics
            await _tokenCache.SetRefreshTokenValidityAsync(refreshToken.Token, true, TimeSpan.FromDays(7));
        }

        public async Task RevokeRefreshTokenAsync(RefreshToken refreshToken, string? replacedByToken = null)
        {
            if (refreshToken == null) throw new ArgumentNullException(nameof(refreshToken));

            refreshToken.Revoked = DateTime.UtcNow;
            refreshToken.ReplacedByToken = replacedByToken;

            // keep a short TTL for denied tokens (matches your code)
            await _tokenCache.SetRefreshTokenValidityAsync(refreshToken.Token, false, TimeSpan.FromDays(1));
        }

        public async Task RemoveOldRefreshTokensAsync(User user)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            var cutoff = DateTime.UtcNow.AddDays(-7);

            lock (GetLock(user.Id))
            {
                if (user.RefreshTokens == null || user.RefreshTokens.Count == 0)
                {
                    // await Task.CompletedTask;
                    return;
                }

                var toRemove = user.RefreshTokens
                    .Where(rt => !rt.IsActive && rt.Created < cutoff)
                    .ToList();

                foreach (var token in toRemove)
                {
                    user.RefreshTokens.Remove(token);
                }
            }

            await Task.CompletedTask;
        }

        public async Task AddRevokedJtiAsync(User user, string jti)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(jti)) throw new ArgumentException("JTI cannot be null/empty.", nameof(jti));

            lock (GetLock(user.Id))
            {
                user.RevokedJtis ??= new HashSet<string>(StringComparer.Ordinal);
                user.RevokedJtis.Add(jti);
            }

            await _tokenCache.AddRevokedJtiAsync(user.Username, jti);
        }

        public async Task<bool> IsJtiRevokedAsync(User user, string jti)
        {
            if (user == null) throw new ArgumentNullException(nameof(user));

            // Fast path: cache
            if (await _tokenCache.IsJtiRevokedAsync(user.Username, jti))
                return true;

            // Fallback: in-memory store
            var exists = (user.RevokedJtis?.Contains(jti) ?? false);

            // Warm cache if needed
            if (exists)
                await _tokenCache.AddRevokedJtiAsync(user.Username, jti);

            return exists;
        }

        // Mimic EF's SaveChangesAsync
        public async Task SaveChangesAsync()
        {
            // No real DB, so just return success (number of "changes" could be approximated)
            await Task.CompletedTask;
        }
    }
}
