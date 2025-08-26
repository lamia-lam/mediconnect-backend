using MedConnect.Models;

namespace MedConnect.Repositories;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByUsernameAsync(string username);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByRefreshTokenAsync(string refreshToken);
    Task AddRefreshTokenAsync(User user, RefreshToken refreshToken);
    Task RevokeRefreshTokenAsync(RefreshToken refreshToken, string? replacedByToken = null);
    Task RemoveOldRefreshTokensAsync(User user);
    Task AddRevokedJtiAsync(User user, string jti);
    Task<bool> IsJtiRevokedAsync(User user, string jti);
}