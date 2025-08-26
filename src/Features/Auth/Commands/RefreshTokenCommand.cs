using MediatR;
using MedConnect.Repositories;
using MedConnect.Services;
using System.Security.Cryptography;

namespace MedConnect.Features.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<(string Jwt, string RefreshToken)?>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, (string Jwt, string RefreshToken)?>
{
    private readonly IUserRepository _userRepository;
    private readonly JwtService _jwtService;

    public RefreshTokenCommandHandler(IUserRepository userRepository, JwtService jwtService)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
    }

    public async Task<(string Jwt, string RefreshToken)?> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByRefreshTokenAsync(request.RefreshToken);
        if (user == null) return null;
        var oldToken = user.RefreshTokens.FirstOrDefault(rt => rt.Token == request.RefreshToken);
        if (oldToken == null || !oldToken.IsActive) return null;
        // Rotate refresh token
        var newRefreshToken = new MedConnect.Models.RefreshToken
        {
            Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            Expires = DateTime.UtcNow.AddDays(7),
            Created = DateTime.UtcNow,
            UserId = user.Id
        };
        await _userRepository.AddRefreshTokenAsync(user, newRefreshToken);
        await _userRepository.RevokeRefreshTokenAsync(oldToken, newRefreshToken.Token);
        await _userRepository.RemoveOldRefreshTokensAsync(user);
        var jwt = _jwtService.GenerateToken(user);
        return (jwt, newRefreshToken.Token);
    }
}