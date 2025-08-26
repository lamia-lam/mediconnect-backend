using Microsoft.EntityFrameworkCore;
using MedConnect.Data;
using MedConnect.Models;
using MedConnect.Services;

namespace MedConnect.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<UserRepository> _logger;
    private readonly ITokenValidationCache _tokenCache;

    public UserRepository(ApplicationDbContext context, ILogger<UserRepository> logger, ITokenValidationCache tokenCache) : base(context, logger)
    {
        _context = context;
        _logger = logger;
        _tokenCache = tokenCache;
    }
    public async Task<User?> GetByRefreshTokenAsync(string refreshToken)
    {
        _logger.LogInformation("Fetching user by refresh token");
        return await _context.Users.Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.RefreshTokens.Any(rt => rt.Token == refreshToken));
    }

    public async Task AddRevokedJtiAsync(User user, string jti)
    {
        if (!user.RevokedJtis.Contains(jti))
        {
            user.RevokedJtis.Add(jti);
            await _context.SaveChangesAsync();
            await _tokenCache.AddRevokedJtiAsync(user.Username, jti);
        }
    }

    public async Task<bool> IsJtiRevokedAsync(User user, string jti)
    {
        return await Task.FromResult(user.RevokedJtis.Contains(jti));
    }

    public async Task AddRefreshTokenAsync(User user, RefreshToken refreshToken)
    {
        user.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();
        await _tokenCache.SetRefreshTokenValidityAsync(refreshToken.Token, true, TimeSpan.FromDays(7));
    }

    public async Task RevokeRefreshTokenAsync(RefreshToken refreshToken, string? replacedByToken = null)
    {
        refreshToken.Revoked = DateTime.UtcNow;
        refreshToken.ReplacedByToken = replacedByToken;
        await _context.SaveChangesAsync();
        await _tokenCache.SetRefreshTokenValidityAsync(refreshToken.Token, false, TimeSpan.FromDays(1));
    }

    public async Task RemoveOldRefreshTokensAsync(User user)
    {
        var oldTokens = user.RefreshTokens.Where(rt => !rt.IsActive && rt.Created < DateTime.UtcNow.AddDays(-7)).ToList();
        foreach (var token in oldTokens)
        {
            _context.RefreshTokens.Remove(token);
        }
        await _context.SaveChangesAsync();
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        _logger.LogInformation("Fetching user by username: {Username}", username);
        return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        _logger.LogInformation("Fetching user by email: {Email}", email);
        return await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
    }
}